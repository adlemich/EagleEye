using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for <c>AppRules</c> and <c>AccountBudgetTexts</c> (coding guidelines §8.2, plain <c>using</c> per §3.4).</summary>
public sealed class AppRuleRepository(ServiceDatabase database) : IAppRuleRepository
{
    /// <inheritdoc />
    public Task<StoredAppRules> LoadAllAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            var apps = new List<StoredAppRecord>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT a.AccountSid, a.AppId, a.ProgramPath, a.DisplayName, r.BudgetMinutes, r.Days
                    FROM AppRecords a LEFT JOIN AppRules r ON r.AppId = a.AppId
                    ORDER BY a.AppId;
                    """;
                using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    var hasRule = !reader.IsDBNull(4);
                    apps.Add(new StoredAppRecord(
                        reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                        hasRule ? reader.GetInt32(4) : AppRuleRules.DefaultBudgetMinutes,
                        hasRule ? (BreakTimeDays)reader.GetInt32(5) : AppRuleRules.DefaultDays));
                }
            }

            var texts = new List<StoredDisplayText>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT AccountSid, Text FROM AccountBudgetTexts ORDER BY AccountSid;";
                using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    texts.Add(new StoredDisplayText(reader.GetString(0), reader.GetString(1)));
                }
            }

            return new StoredAppRules(apps, texts);
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> SetRuleAsync(
        string accountSid, long appId, int budgetMinutes, BreakTimeDays days, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            using (var exists = connection.CreateCommand())
            {
                exists.Transaction = transaction;
                exists.CommandText = "SELECT COUNT(*) FROM AppRecords WHERE AppId = @id AND AccountSid = @sid COLLATE NOCASE;";
                exists.Parameters.AddWithValue("@id", appId);
                exists.Parameters.AddWithValue("@sid", accountSid);
                if ((long)(await exists.ExecuteScalarAsync(token).ConfigureAwait(false))! == 0) // COUNT always yields a value.
                {
                    return false;
                }
            }

            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            if (budgetMinutes == AppRuleRules.DefaultBudgetMinutes && days == AppRuleRules.DefaultDays)
            {
                write.CommandText = "DELETE FROM AppRules WHERE AppId = @id;";
            }
            else
            {
                write.CommandText = """
                    INSERT INTO AppRules (AppId, BudgetMinutes, Days, ChangedUtc) VALUES (@id, @budget, @days, @now)
                    ON CONFLICT (AppId) DO UPDATE SET BudgetMinutes = excluded.BudgetMinutes, Days = excluded.Days, ChangedUtc = excluded.ChangedUtc;
                    """;
                write.Parameters.AddWithValue("@budget", budgetMinutes);
                write.Parameters.AddWithValue("@days", (int)days);
                write.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            }

            write.Parameters.AddWithValue("@id", appId);
            await write.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, ct);
    }

    /// <inheritdoc />
    public Task SetBudgetTextAsync(string accountSid, string? text, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            if (text is null)
            {
                command.CommandText = "DELETE FROM AccountBudgetTexts WHERE AccountSid = @sid COLLATE NOCASE;";
            }
            else
            {
                command.CommandText = """
                    INSERT INTO AccountBudgetTexts (AccountSid, Text, ChangedUtc) VALUES (@sid, @text, @now)
                    ON CONFLICT (AccountSid) DO UPDATE SET Text = excluded.Text, ChangedUtc = excluded.ChangedUtc;
                    """;
                command.Parameters.AddWithValue("@text", text);
                command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            }

            command.Parameters.AddWithValue("@sid", accountSid);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PurgeBudgetTextsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return database.ExecuteAsync<IReadOnlyList<string>>(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                DELETE FROM AccountBudgetTexts WHERE AccountSid COLLATE NOCASE NOT IN ({SqlValues.InList(command, existingSids)})
                RETURNING AccountSid;
                """;
            var purged = new List<string>();
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                purged.Add(reader.GetString(0));
            }

            return purged;
        }, ct);
    }
}
