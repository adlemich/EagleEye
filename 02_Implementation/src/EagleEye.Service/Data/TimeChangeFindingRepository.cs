namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for <c>TimeChangeFindings</c> (coding guidelines §8.2, plain <c>using</c> per §3.4).</summary>
public sealed class TimeChangeFindingRepository(ServiceDatabase database) : ITimeChangeFindingRepository
{
    /// <inheritdoc />
    public Task InsertAsync(TimeChangeFinding finding, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO TimeChangeFindings (DetectedUtc, DetectedLocal, Kind, OldValue, NewValue, SessionId, AccountSid, UserName)
                VALUES (@detected, @local, @kind, @old, @new, @session, @sid, @user);
                """;
            command.Parameters.AddWithValue("@detected", SqlValues.Utc(finding.DetectedUtc));
            command.Parameters.AddWithValue("@local", SqlValues.Local(finding.DetectedLocal));
            command.Parameters.AddWithValue("@kind", finding.Kind);
            command.Parameters.AddWithValue("@old", finding.OldValue);
            command.Parameters.AddWithValue("@new", finding.NewValue);
            command.Parameters.AddWithValue("@session", (object?)finding.SessionId ?? DBNull.Value);
            command.Parameters.AddWithValue("@sid", (object?)finding.AccountSid ?? DBNull.Value);
            command.Parameters.AddWithValue("@user", (object?)finding.UserName ?? DBNull.Value);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TimeChangeFindings WHERE DetectedUtc < @cutoff;";
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
            command.CommandText = $"""
                DELETE FROM TimeChangeFindings
                WHERE AccountSid IS NOT NULL AND AccountSid COLLATE NOCASE NOT IN ({SqlValues.InList(command, existingSids)});
                """;
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }
}
