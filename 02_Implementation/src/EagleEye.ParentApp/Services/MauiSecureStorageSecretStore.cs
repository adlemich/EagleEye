using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>
/// Secret store for Android, iOS and macOS via MAUI <see cref="SecureStorage"/> (Keystore /
/// Keychain, coding guidelines §14, §15). Not used on Windows, where the app is unpackaged and
/// <see cref="SecureStorage"/> needs package identity (ADR-009). Compiled, not exercised in US-002.
/// </summary>
public sealed class MauiSecureStorageSecretStore : ISecretStore
{
    /// <inheritdoc />
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    /// <inheritdoc />
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
