namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL for the single row of <c>WarningSettings</c> (coding guidelines §8.2).</summary>
public sealed class WarningSettingsRepository(ServiceDatabase database) : IWarningSettingsRepository
{
    /// <inheritdoc />
    public Task<StoredWarningSettings?> LoadAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EarlyMinutes, FinalMinutes FROM WarningSettings WHERE Id = 1;";
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false))
            {
                return null;
            }

            return new StoredWarningSettings(reader.GetInt32(0), reader.GetInt32(1));
        }, ct);
    }

    /// <inheritdoc />
    public Task SaveAsync(StoredWarningSettings settings, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO WarningSettings (Id, EarlyMinutes, FinalMinutes, ChangedUtc)
                VALUES (1, @early, @final, @now)
                ON CONFLICT (Id) DO UPDATE SET EarlyMinutes = excluded.EarlyMinutes, FinalMinutes = excluded.FinalMinutes,
                    ChangedUtc = excluded.ChangedUtc;
                """;
            command.Parameters.AddWithValue("@early", settings.EarlyMinutes);

            command.Parameters.AddWithValue("@final", settings.FinalMinutes);

            command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }
}
