using EagleEye.ParentApp.Core.Abstractions;
using Foundation;
using Security;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// macOS secret store on the Security framework (ADR-020 §2): generic password, service
/// <see cref="Service"/>, account = key, label "EagleEye Parent App", not synchronisable, variant
/// K-1 (file-based login keychain). Replaces MAUI <c>SecureStorage</c>, which needs a
/// <c>keychain-access-groups</c> entitlement that an ad-hoc build cannot carry (US-007 first light).
/// Not unit-tested (platform code); verified manually.
/// </summary>
public sealed class MacKeychainSecretStore : ISecretStore
{
    /// <summary>Keychain service of all EagleEye items (= bundle ID).</summary>
    public const string Service = "com.adlemich.eagleeye";

    private const string Label = "EagleEye Parent App";

    // K-1: the login keychain. The Step 2 spike may switch this to the data-protection keychain (K-2).
    private const bool UseDataProtectionKeychain = false;

    /// <inheritdoc />
    public Task<string?> GetAsync(string key)
    {
        var query = CreateQuery(key);
        using var data = SecKeyChain.QueryAsData(query, false, out var status);
        return status switch
        {
            SecStatusCode.Success when data is not null => Task.FromResult<string?>(data.ToString(NSStringEncoding.UTF8).ToString()),
            SecStatusCode.ItemNotFound => Task.FromResult<string?>(null),
            _ => throw new SecretStoreUnavailableException("read", (int)status),
        };
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var data = NSData.FromString(value, NSStringEncoding.UTF8);

        var status = SecKeyChain.Update(CreateQuery(key), new SecRecord(SecKind.GenericPassword) { ValueData = data });
        if (status == SecStatusCode.ItemNotFound)
        {
            var record = CreateQuery(key);
            record.Label = Label;
            record.ValueData = data;
            status = SecKeyChain.Add(record);
        }

        return status == SecStatusCode.Success
            ? Task.CompletedTask
            : throw new SecretStoreUnavailableException("write", (int)status);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        var status = SecKeyChain.Remove(CreateQuery(key));
        return status is SecStatusCode.Success or SecStatusCode.ItemNotFound
            ? Task.CompletedTask
            : throw new SecretStoreUnavailableException("delete", (int)status);
    }

    private static SecRecord CreateQuery(string key) => new(SecKind.GenericPassword)
    {
        Service = Service,
        Account = key,
        Synchronizable = false,
        UseDataProtectionKeychain = UseDataProtectionKeychain,
    };
}
