using System.Text;
using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Core.Data;

/// <summary>
/// <see cref="ISecretStore"/> that keeps secrets encrypted by an <see cref="ISecretProtector"/> in
/// the <c>Secrets</c> table. Used on Windows with DPAPI CurrentUser, because MAUI
/// <c>SecureStorage</c> needs package identity and the Windows app is unpackaged (ADR-009).
/// </summary>
public sealed class ProtectedSecretStore(ParentDatabase database, ISecretProtector protector) : ISecretStore
{
    /// <inheritdoc />
    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var protectedValue = await database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ProtectedValue FROM Secrets WHERE Key = @key;";
            command.Parameters.AddWithValue("@key", key);
            return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as byte[];
        }).ConfigureAwait(false);

        return protectedValue is null ? null : Encoding.UTF8.GetString(protector.Unprotect(protectedValue));
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var protectedValue = protector.Protect(Encoding.UTF8.GetBytes(value));
        return database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Secrets (Key, ProtectedValue) VALUES (@key, @value)
                ON CONFLICT (Key) DO UPDATE SET ProtectedValue = excluded.ProtectedValue;
                """;
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", protectedValue);
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return database.ExecuteAsync(async (connection, ct) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Secrets WHERE Key = @key;";
            command.Parameters.AddWithValue("@key", key);
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        });
    }
}
