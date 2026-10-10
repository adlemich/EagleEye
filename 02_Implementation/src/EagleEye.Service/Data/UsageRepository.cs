using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EagleEye.Service.Data;

/// <summary>
/// Parameterized SQL for the usage tables (coding guidelines §8.2, plain <c>using</c> per §3.4). Times are stored
/// as ISO 8601 UTC (<c>O</c> format, so they compare as text), days as <c>yyyy-MM-dd</c>.
/// </summary>
public sealed class UsageRepository(ServiceDatabase database) : IUsageRepository
{
    private const string DayFormat = "yyyy-MM-dd";

    /// <inheritdoc />
    public Task<AppRecordResult> GetOrCreateAppAsync(
        string accountSid, string programPath, string processName, string displayName, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return database.ExecuteAsync(async (connection, token) =>
        {
            using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE AppRecords SET ProcessName = @process, DisplayName = @display, LastSeenUtc = @now
                WHERE AccountSid = @sid COLLATE NOCASE AND ProgramPath = @path
                RETURNING AppId;
                """;
            AddAppParameters(update, accountSid, programPath, processName, displayName, nowUtc);
            if (await update.ExecuteScalarAsync(token).ConfigureAwait(false) is long existing)
            {
                return new AppRecordResult(existing, false);
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO AppRecords (AccountSid, ProgramPath, ProcessName, DisplayName, FirstSeenUtc, LastSeenUtc)
                VALUES (@sid, @path, @process, @display, @now, @now)
                RETURNING AppId;
                """;
            AddAppParameters(insert, accountSid, programPath, processName, displayName, nowUtc);
            var created = (long)(await insert.ExecuteScalarAsync(token).ConfigureAwait(false))!; // RETURNING always yields the id.
            return new AppRecordResult(created, true);
        }, ct);
    }

    /// <inheritdoc />
    public Task<long> StartInstanceAsync(long appId, DateTimeOffset startedUtc, CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AppInstances (AppId, StartedUtc, LastSeenUtc) VALUES (@app, @start, @start)
                RETURNING InstanceId;
                """;
            command.Parameters.AddWithValue("@app", appId);
            command.Parameters.AddWithValue("@start", Utc(startedUtc));
            return (long)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!; // RETURNING always yields the id.
        }, ct);
    }

    /// <inheritdoc />
    public Task EndInstanceAsync(long instanceId, DateTimeOffset endedUtc, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE AppInstances SET EndedUtc = @end, LastSeenUtc = @end, EndReason = @reason
                WHERE InstanceId = @id AND EndedUtc IS NULL;
                """;
            command.Parameters.AddWithValue("@id", instanceId);
            command.Parameters.AddWithValue("@end", Utc(endedUtc));
            command.Parameters.AddWithValue("@reason", reason);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OpenInstanceRecord>> GetOpenInstancesAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync<IReadOnlyList<OpenInstanceRecord>>(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT i.InstanceId, a.AccountSid, a.ProgramPath, a.ProcessName, a.DisplayName, i.StartedUtc, i.LastSeenUtc
                FROM AppInstances i JOIN AppRecords a ON a.AppId = i.AppId
                WHERE i.EndedUtc IS NULL ORDER BY i.InstanceId;
                """;
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var open = new List<OpenInstanceRecord>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                open.Add(new OpenInstanceRecord(
                    reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    ParseUtc(reader.GetString(5)), ParseUtc(reader.GetString(6))));
            }

            return open;
        }, ct);
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyCollection<UsageIncrement> increments, IReadOnlyCollection<InstanceSeenRecord> seen, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(increments);
        ArgumentNullException.ThrowIfNull(seen);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            foreach (var increment in increments)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO DailyUsage (AppId, Day, Seconds) VALUES (@app, @day, @seconds)
                    ON CONFLICT (AppId, Day) DO UPDATE SET Seconds = Seconds + excluded.Seconds;
                    """;
                command.Parameters.AddWithValue("@app", increment.AppId);
                command.Parameters.AddWithValue("@day", Day(increment.Day));
                command.Parameters.AddWithValue("@seconds", increment.Seconds);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            foreach (var instance in seen)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "UPDATE AppInstances SET LastSeenUtc = @seen WHERE InstanceId = @id AND EndedUtc IS NULL;";
                command.Parameters.AddWithValue("@id", instance.InstanceId);
                command.Parameters.AddWithValue("@seen", Utc(instance.LastSeenUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DayAppUsage>> GetUsageAsync(string accountSid, DateOnly fromDay, DateOnly toDay, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        return database.ExecuteAsync<IReadOnlyList<DayAppUsage>>(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT d.Day, a.AppId, a.DisplayName, d.Seconds
                FROM DailyUsage d JOIN AppRecords a ON a.AppId = d.AppId
                WHERE a.AccountSid = @sid COLLATE NOCASE AND d.Day >= @from AND d.Day <= @to
                ORDER BY d.Day DESC, a.AppId;
                """;
            command.Parameters.AddWithValue("@sid", accountSid);
            command.Parameters.AddWithValue("@from", Day(fromDay));
            command.Parameters.AddWithValue("@to", Day(toDay));
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var rows = new List<DayAppUsage>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                rows.Add(new DayAppUsage(
                    DateOnly.ParseExact(reader.GetString(0), DayFormat, CultureInfo.InvariantCulture),
                    reader.GetInt64(1), reader.GetString(2), reader.GetInt64(3)));
            }

            return rows;
        }, ct);
    }

    /// <inheritdoc />
    public Task<(int DayRows, int Instances)> PurgeOlderThanAsync(DateOnly cutoffDay, DateTimeOffset cutoffUtc, CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            using var days = connection.CreateCommand();
            days.Transaction = transaction;
            days.CommandText = "DELETE FROM DailyUsage WHERE Day < @day;";
            days.Parameters.AddWithValue("@day", Day(cutoffDay));
            var dayRows = await days.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            using var instances = connection.CreateCommand();
            instances.Transaction = transaction;
            instances.CommandText = "DELETE FROM AppInstances WHERE StartedUtc < @start AND EndedUtc IS NOT NULL;";
            instances.Parameters.AddWithValue("@start", Utc(cutoffUtc));
            var instanceRows = await instances.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await transaction.CommitAsync(token).ConfigureAwait(false);
            return (dayRows, instanceRows);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountPurge>> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return database.ExecuteAsync<IReadOnlyList<AccountPurge>>(async (connection, token) =>
        {
            using var transaction = connection.BeginTransaction();
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText = $"""
                SELECT a.AccountSid, COUNT(*),
                    (SELECT COUNT(*) FROM AppInstances i JOIN AppRecords r ON r.AppId = i.AppId WHERE r.AccountSid = a.AccountSid),
                    (SELECT COUNT(*) FROM DailyUsage d JOIN AppRecords r ON r.AppId = d.AppId WHERE r.AccountSid = a.AccountSid)
                FROM AppRecords a WHERE a.AccountSid COLLATE NOCASE NOT IN ({InList(count, existingSids)})
                GROUP BY a.AccountSid ORDER BY a.AccountSid;
                """;
            var purged = new List<AccountPurge>();
            using (var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    purged.Add(new AccountPurge(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
                }
            }

            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM AppRecords WHERE AccountSid COLLATE NOCASE NOT IN ({InList(delete, existingSids)});";
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return purged;
        }, ct);
    }

    /// <summary>Adds one parameter per value and returns the parameter list for an <c>IN (…)</c> clause.</summary>
    private static string InList(SqliteCommand command, IEnumerable<string> values)
    {
        var names = new List<string>();
        foreach (var value in values)
        {
            var name = "@p" + names.Count.ToString(CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(name, value);
            names.Add(name);
        }

        return string.Join(", ", names);
    }

    private static void AddAppParameters(
        SqliteCommand command, string sid, string path, string processName, string displayName, DateTimeOffset nowUtc)
    {
        command.Parameters.AddWithValue("@sid", sid);
        command.Parameters.AddWithValue("@path", path);
        command.Parameters.AddWithValue("@process", processName);
        command.Parameters.AddWithValue("@display", displayName);
        command.Parameters.AddWithValue("@now", Utc(nowUtc));
    }

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Day(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);
}
