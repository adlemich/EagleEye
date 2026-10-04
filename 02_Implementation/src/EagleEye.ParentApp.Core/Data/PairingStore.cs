using System.Globalization;
using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Core.Data;

/// <summary>
/// Persists the single pairing: host, device ID, device name, pinned thumbprint and timestamp in
/// <c>ServerConnections</c>, the token via <see cref="ISecretStore"/> under <see cref="TokenKey"/>.
/// A row without a token counts as not paired.
/// </summary>
public sealed class PairingStore(ParentDatabase database, ISecretStore secrets) : IPairingStore
{
    /// <summary>Secret-store key of the pairing token.</summary>
    public const string TokenKey = "pairing-token";

    /// <inheritdoc />
    public async Task<StoredPairing?> LoadAsync()
    {
        var row = await database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Host, DeviceId, DeviceName, CertificateThumbprint, PairedAtUtc
                FROM ServerConnections LIMIT 1;
                """;
            using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return new PairingRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var token = await secrets.GetAsync(TokenKey).ConfigureAwait(false);
        return token is null
            ? null
            : new StoredPairing(row.Host, row.DeviceId, row.DeviceName, row.Thumbprint, row.PairedAtUtc, token);
    }

    /// <inheritdoc />
    public async Task SaveAsync(StoredPairing pairing)
    {
        ArgumentNullException.ThrowIfNull(pairing);

        await secrets.SetAsync(TokenKey, pairing.Token).ConfigureAwait(false);
        await database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM ServerConnections;
                INSERT INTO ServerConnections (Host, DeviceId, DeviceName, CertificateThumbprint, PairedAtUtc)
                VALUES (@host, @id, @name, @thumbprint, @pairedAt);
                """;
            command.Parameters.AddWithValue("@host", pairing.Host);
            command.Parameters.AddWithValue("@id", pairing.DeviceId);
            command.Parameters.AddWithValue("@name", pairing.DeviceName);
            command.Parameters.AddWithValue("@thumbprint", pairing.CertificateThumbprint);
            command.Parameters.AddWithValue("@pairedAt", pairing.PairedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync()
    {
        await database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ServerConnections;";
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await secrets.RemoveAsync(TokenKey).ConfigureAwait(false);
    }

    private sealed record PairingRow(string Host, string DeviceId, string DeviceName, string Thumbprint, DateTimeOffset PairedAtUtc);
}
