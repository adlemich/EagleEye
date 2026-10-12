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
            command.CommandText = "SELECT EarlyMinutes, EarlyText, FinalMinutes, FinalText FROM WarningSettings WHERE Id = 1;";
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false))
            {
                return null;
            }

            return new StoredWarningSettings(
                reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3));
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
                INSERT INTO WarningSettings (Id, EarlyMinutes, EarlyText, FinalMinutes, FinalText, ChangedUtc)
                VALUES (1, @early, @earlyText, @final, @finalText, @now)
                ON CONFLICT (Id) DO UPDATE SET EarlyMinutes = excluded.EarlyMinutes, EarlyText = excluded.EarlyText,
                    FinalMinutes = excluded.FinalMinutes, FinalText = excluded.FinalText, ChangedUtc = excluded.ChangedUtc;
                """;
            command.Parameters.AddWithValue("@early", settings.EarlyMinutes);
            command.Parameters.AddWithValue("@earlyText", (object?)settings.EarlyText ?? DBNull.Value);
            command.Parameters.AddWithValue("@final", settings.FinalMinutes);
            command.Parameters.AddWithValue("@finalText", (object?)settings.FinalText ?? DBNull.Value);
            command.Parameters.AddWithValue("@now", SqlValues.Utc(nowUtc));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }
}
