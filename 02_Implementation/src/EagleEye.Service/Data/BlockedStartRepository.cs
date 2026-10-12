namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for <c>BlockedStarts</c> (coding guidelines §8.2, plain <c>using</c> per §3.4).</summary>
public sealed class BlockedStartRepository(ServiceDatabase database) : IBlockedStartRepository
{
    /// <inheritdoc />
    public Task<long> InsertAsync(BlockedStartRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO BlockedStarts (Kind, Reason, AfterGrace, AccountSid, UserName, StartedUtc, StartedLocal, DetectedUtc,
                    DisplayName, ProcessName, ProgramPath, ProcessId, Trigger, EntryId, EntryStartMinute, EntryEndMinute, EntryDays,
                    BudgetMinutes, BudgetDays, UsedSeconds, Weekday)
                VALUES (@kind, @reason, @afterGrace, @sid, @user, @started, @startedLocal, @detected,
                    @display, @process, @path, @pid, @trigger, @entry, @entryStart, @entryEnd, @entryDays,
                    @budget, @budgetDays, @used, @weekday)
                RETURNING BlockedStartId;
                """;
            command.Parameters.AddWithValue("@kind", record.Kind);
            command.Parameters.AddWithValue("@reason", record.Reason);
            command.Parameters.AddWithValue("@afterGrace", record.AfterGrace ? 1 : 0);
            command.Parameters.AddWithValue("@sid", record.AccountSid);
            command.Parameters.AddWithValue("@user", record.UserName);
            command.Parameters.AddWithValue("@started", SqlValues.Utc(record.StartedUtc));
            command.Parameters.AddWithValue("@startedLocal", SqlValues.Local(record.StartedLocal));
            command.Parameters.AddWithValue("@detected", SqlValues.Utc(record.DetectedUtc));
            command.Parameters.AddWithValue("@display", record.DisplayName);
            command.Parameters.AddWithValue("@process", record.ProcessName);
            command.Parameters.AddWithValue("@path", record.ProgramPath);
            command.Parameters.AddWithValue("@pid", record.ProcessId);
            command.Parameters.AddWithValue("@trigger", record.Trigger);
            command.Parameters.AddWithValue("@entry", (object?)record.Entry?.EntryId ?? DBNull.Value);
            command.Parameters.AddWithValue("@entryStart", (object?)record.Entry?.StartMinute ?? DBNull.Value);
            command.Parameters.AddWithValue("@entryEnd", (object?)record.Entry?.EndMinute ?? DBNull.Value);
            command.Parameters.AddWithValue("@entryDays", record.Entry is null ? DBNull.Value : (object)(int)record.Entry.Days);
            command.Parameters.AddWithValue("@budget", (object?)record.Rule?.BudgetMinutes ?? DBNull.Value);
            command.Parameters.AddWithValue("@budgetDays", record.Rule is null ? DBNull.Value : (object)(int)record.Rule.Days);
            command.Parameters.AddWithValue("@used", (object?)record.Rule?.UsedSeconds ?? DBNull.Value);
            command.Parameters.AddWithValue("@weekday", record.Weekday);
            return (long)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!; // RETURNING always yields the id.
        }, ct);
    }

    /// <inheritdoc />
    public Task CompleteAsync(
        long blockedStartId, string outcome, double? secondsUntilGone, string messageState, DateTimeOffset completedUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageState);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE BlockedStarts SET Outcome = @outcome, SecondsUntilGone = @seconds, MessageState = @message, CompletedUtc = @completed
                WHERE BlockedStartId = @id;
                """;
            command.Parameters.AddWithValue("@id", blockedStartId);
            command.Parameters.AddWithValue("@outcome", outcome);
            command.Parameters.AddWithValue("@seconds", (object?)secondsUntilGone ?? DBNull.Value);
            command.Parameters.AddWithValue("@message", messageState);
            command.Parameters.AddWithValue("@completed", SqlValues.Utc(completedUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<int> CompleteDanglingAsync(string outcome, DateTimeOffset completedUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE BlockedStarts SET Outcome = @outcome, CompletedUtc = @completed WHERE Outcome IS NULL;";
            command.Parameters.AddWithValue("@outcome", outcome);
            command.Parameters.AddWithValue("@completed", SqlValues.Utc(completedUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM BlockedStarts WHERE DetectedUtc < @cutoff;";
            command.Parameters.AddWithValue("@cutoff", SqlValues.Utc(cutoffUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<int> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM BlockedStarts WHERE AccountSid COLLATE NOCASE NOT IN ({SqlValues.InList(command, existingSids)});";
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }
}
