using System.Globalization;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;

namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for the app group tables (coding guidelines §8.2, plain <c>using</c> per §3.4).</summary>
public sealed class AppGroupRepository(ServiceDatabase database) : IAppGroupRepository
{
    /// <inheritdoc />
    public Task<StoredAppGroups> LoadAllAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            var groups = await ReadAsync(
                connection, "SELECT GroupId, Name FROM AppGroups ORDER BY GroupId;",
                r => new StoredAppGroup(r.GetInt64(0), r.GetString(1)), token).ConfigureAwait(false);
            var members = await ReadAsync(
                connection, "SELECT ProgramPath, GroupId FROM AppGroupMembers ORDER BY ProgramPath;",
                r => new StoredAppGroupMember(r.GetString(0), r.GetInt64(1)), token).ConfigureAwait(false);
            var rules = await ReadAsync(
                connection, "SELECT GroupId, AccountSid, BudgetMinutes, Days FROM AppGroupRules ORDER BY GroupId, AccountSid;",
                r => new StoredAppGroupRule(r.GetInt64(0), r.GetString(1), r.GetInt32(2), (BreakTimeDays)r.GetInt32(3)), token).ConfigureAwait(false);
            var catalog = await ReadAsync(
                connection, "SELECT AccountSid, ProgramPath, DisplayName, LastSeenUtc FROM AppRecords ORDER BY AppId;",
                r => new StoredCatalogApp(
                    r.GetString(0), r.GetString(1), r.GetString(2),
                    DateTimeOffset.Parse(r.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)), token).ConfigureAwait(false);
            return new StoredAppGroups(groups, members, rules, catalog);
        }, ct);
    }

    /// <inheritdoc />
    public Task<long> CreateGroupAsync(string name, string nameKey, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameKey);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AppGroups (Name, NameKey, CreatedUtc, ChangedUtc) VALUES (@name, @key, @now, @now)
                RETURNING GroupId;
                """;
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@key", nameKey);
            command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            return (long)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!; // RETURNING always yields the id.
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> RenameGroupAsync(long groupId, string name, string nameKey, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameKey);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE AppGroups SET Name = @name, NameKey = @key, ChangedUtc = @now WHERE GroupId = @id;";
            command.Parameters.AddWithValue("@id", groupId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@key", nameKey);
            command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> DeleteGroupAsync(long groupId, CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM AppGroups WHERE GroupId = @id;";
            command.Parameters.AddWithValue("@id", groupId);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }, ct);
    }

    /// <inheritdoc />
    public Task SetMemberAsync(string programPath, long? groupId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            if (groupId is { } id)
            {
                command.CommandText = """
                    INSERT INTO AppGroupMembers (ProgramPath, GroupId, ChangedUtc) VALUES (@path, @group, @now)
                    ON CONFLICT (ProgramPath) DO UPDATE SET GroupId = excluded.GroupId, ChangedUtc = excluded.ChangedUtc;
                    """;
                command.Parameters.AddWithValue("@group", id);
                command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            }
            else
            {
                command.CommandText = "DELETE FROM AppGroupMembers WHERE ProgramPath = @path;";
            }

            command.Parameters.AddWithValue("@path", programPath);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task SetRuleAsync(long groupId, string accountSid, int budgetMinutes, BreakTimeDays days, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            if (budgetMinutes == AppRuleRules.DefaultBudgetMinutes && days == AppRuleRules.DefaultDays)
            {
                command.CommandText = "DELETE FROM AppGroupRules WHERE GroupId = @group AND AccountSid = @sid;";
            }
            else
            {
                command.CommandText = """
                    INSERT INTO AppGroupRules (GroupId, AccountSid, BudgetMinutes, Days, ChangedUtc) VALUES (@group, @sid, @budget, @days, @now)
                    ON CONFLICT (GroupId, AccountSid) DO UPDATE SET BudgetMinutes = excluded.BudgetMinutes, Days = excluded.Days, ChangedUtc = excluded.ChangedUtc;
                    """;
                command.Parameters.AddWithValue("@budget", budgetMinutes);
                command.Parameters.AddWithValue("@days", (int)days);
                command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            }

            command.Parameters.AddWithValue("@group", groupId);
            command.Parameters.AddWithValue("@sid", accountSid);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PurgeRulesNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return database.ExecuteAsync<IReadOnlyList<string>>(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM AppGroupRules WHERE AccountSid NOT IN ({SqlValues.InList(command, existingSids)}) RETURNING AccountSid;";
            var purged = new List<string>();
            using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    purged.Add(reader.GetString(0));
                }
            }

            return [.. purged.Distinct(StringComparer.OrdinalIgnoreCase)];
        }, ct);
    }

    private static async Task<List<T>> ReadAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
