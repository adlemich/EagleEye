using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;

namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for the rules tables (coding guidelines §8.2, plain <c>using</c> per §3.4).</summary>
public sealed class BreakTimeRepository(ServiceDatabase database) : IBreakTimeRepository
{
    /// <inheritdoc />
    public Task<StoredRules> LoadAllAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            var entries = new List<StoredBreakTimeEntry>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT AccountSid, EntryId, IsActive, StartMinute, EndMinute, Days FROM BreakTimeEntries ORDER BY EntryId;
                    """;
                using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    entries.Add(new StoredBreakTimeEntry(
                        reader.GetString(0),
                        new BreakTimeEntryDto(reader.GetInt64(1), reader.GetInt64(2) == 1, reader.GetInt32(3), reader.GetInt32(4), (BreakTimeDays)reader.GetInt32(5))));
                }
            }

            var texts = new List<StoredDisplayText>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT AccountSid, Text FROM AccountDisplayTexts ORDER BY AccountSid;";
                using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    texts.Add(new StoredDisplayText(reader.GetString(0), reader.GetString(1)));
                }
            }

            return new StoredRules(entries, texts);
        }, ct);
    }

    /// <inheritdoc />
    public Task<long> InsertEntryAsync(string accountSid, BreakTimeEntryDto values, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentNullException.ThrowIfNull(values);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO BreakTimeEntries (AccountSid, IsActive, StartMinute, EndMinute, Days, CreatedUtc, ChangedUtc)
                VALUES (@sid, @active, @start, @end, @days, @now, @now)
                RETURNING EntryId;
                """;
            AddEntryParameters(command, accountSid, values, nowUtc);
            return (long)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!; // RETURNING always yields the id.
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> UpdateEntryAsync(string accountSid, BreakTimeEntryDto entry, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentNullException.ThrowIfNull(entry);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE BreakTimeEntries
                SET IsActive = @active, StartMinute = @start, EndMinute = @end, Days = @days, ChangedUtc = @now
                WHERE EntryId = @id AND AccountSid = @sid COLLATE NOCASE;
                """;
            AddEntryParameters(command, accountSid, entry, nowUtc);
            command.Parameters.AddWithValue("@id", entry.EntryId);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> DeleteEntryAsync(string accountSid, long entryId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM BreakTimeEntries WHERE EntryId = @id AND AccountSid = @sid COLLATE NOCASE;";
            command.Parameters.AddWithValue("@id", entryId);
            command.Parameters.AddWithValue("@sid", accountSid);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }, ct);
    }

    /// <inheritdoc />
    public Task SetDisplayTextAsync(string accountSid, string? text, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            if (text is null)
            {
                command.CommandText = "DELETE FROM AccountDisplayTexts WHERE AccountSid = @sid COLLATE NOCASE;";
            }
            else
            {
                command.CommandText = """
                    INSERT INTO AccountDisplayTexts (AccountSid, Text, ChangedUtc) VALUES (@sid, @text, @now)
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
    public Task<IReadOnlyList<RulesPurge>> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return database.ExecuteAsync<IReadOnlyList<RulesPurge>>(async (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            var list = SqlValues.InList(count, existingSids);
            count.CommandText = $"""
                SELECT s.AccountSid,
                    (SELECT COUNT(*) FROM BreakTimeEntries e WHERE e.AccountSid = s.AccountSid),
                    (SELECT COUNT(*) FROM AccountDisplayTexts t WHERE t.AccountSid = s.AccountSid)
                FROM (SELECT AccountSid FROM BreakTimeEntries UNION SELECT AccountSid FROM AccountDisplayTexts) s
                WHERE s.AccountSid COLLATE NOCASE NOT IN ({list})
                ORDER BY s.AccountSid;
                """;
            var purged = new List<RulesPurge>();
            using (var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    purged.Add(new RulesPurge(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2) > 0));
                }
            }

            await DeleteNotInAsync(connection, transaction, "BreakTimeEntries", existingSids, token).ConfigureAwait(false);
            await DeleteNotInAsync(connection, transaction, "AccountDisplayTexts", existingSids, token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return purged;
        }, ct);
    }

    private static async Task DeleteNotInAsync(
        SqliteConnection connection, SqliteTransaction transaction, string table, IReadOnlyCollection<string> existingSids, CancellationToken ct)
    {
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;

        // The table name is one of two constants of this class, never input.
        delete.CommandText = $"DELETE FROM {table} WHERE AccountSid COLLATE NOCASE NOT IN ({SqlValues.InList(delete, existingSids)});";
        await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void AddEntryParameters(SqliteCommand command, string accountSid, BreakTimeEntryDto entry, DateTimeOffset nowUtc)
    {
        command.Parameters.AddWithValue("@sid", accountSid);
        command.Parameters.AddWithValue("@active", entry.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@start", entry.StartMinute);
        command.Parameters.AddWithValue("@end", entry.EndMinute);
        command.Parameters.AddWithValue("@days", (int)entry.Days);
        command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
    }
}
