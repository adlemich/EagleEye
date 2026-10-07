using System.Globalization;

namespace EagleEye.Service.Data;

/// <summary>Parameterized SQL access to <c>PairedDevices</c> (coding guidelines §8.2).</summary>
public sealed class PairedDeviceRepository(ServiceDatabase database) : IPairedDeviceRepository
{
    /// <inheritdoc />
    public Task AddAsync(PairedDevice device, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO PairedDevices (DeviceId, DeviceName, TokenHash, PairedAtUtc)
                VALUES (@id, @name, @hash, @pairedAt);
                """;
            command.Parameters.AddWithValue("@id", device.DeviceId);
            command.Parameters.AddWithValue("@name", device.DeviceName);
            command.Parameters.AddWithValue("@hash", device.TokenHash);
            command.Parameters.AddWithValue("@pairedAt", device.PairedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<PairedDevice?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DeviceId, DeviceName, TokenHash, PairedAtUtc
                FROM PairedDevices WHERE TokenHash = @hash;
                """;
            command.Parameters.AddWithValue("@hash", tokenHash);
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false))
            {
                return null;
            }

            return new PairedDevice(
                reader.GetString(0),
                reader.GetString(1),
                (byte[])reader.GetValue(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }, ct);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string deviceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PairedDevices WHERE DeviceId = @id;";
            command.Parameters.AddWithValue("@id", deviceId);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct);
    }
}
