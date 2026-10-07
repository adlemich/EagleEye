namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>
/// Platform secret storage for the pairing token (ADR-008 §5). Windows: DPAPI CurrentUser,
/// other platforms: Keychain / Keystore via MAUI <c>SecureStorage</c>.
/// </summary>
public interface ISecretStore
{
    /// <summary>Returns the secret, or <c>null</c> if none is stored.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Stores or replaces the secret.</summary>
    Task SetAsync(string key, string value);

    /// <summary>Removes the secret if it exists.</summary>
    Task RemoveAsync(string key);
}
