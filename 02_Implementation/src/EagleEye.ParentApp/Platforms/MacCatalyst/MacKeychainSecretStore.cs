using System.Runtime.InteropServices;
using System.Text;
using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// macOS secret store in the file-based <b>login keychain</b> (ADR-020 §2, variant K-1): generic
/// password, service <see cref="Service"/>, account = key. Uses the legacy <c>SecKeychain…</c> API of
/// Security.framework through P/Invoke, because on Mac Catalyst the <c>SecItem…</c> API always goes to
/// the data-protection keychain and returns <c>errSecMissingEntitlement</c> (-34018) for every write
/// of an ad-hoc-signed app (US-007 first light, FL-2). The token is never logged. Not unit-tested
/// (platform code); verified manually.
/// </summary>
public sealed class MacKeychainSecretStore : ISecretStore
{
    /// <summary>Keychain service of all EagleEye items (= bundle ID).</summary>
    public const string Service = "com.adlemich.eagleeye";

    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Success = 0;
    private const int ItemNotFound = -25300;

    private static readonly byte[] ServiceBytes = Encoding.UTF8.GetBytes(Service);

    /// <inheritdoc />
    public Task<string?> GetAsync(string key)
    {
        var account = Encoding.UTF8.GetBytes(key);
        var status = Find(account, out var length, out var data, out var item);
        Trace("read", status);
        if (status == ItemNotFound)
        {
            return Task.FromResult<string?>(null);
        }

        if (status != Success)
        {
            throw new SecretStoreUnavailableException("read", status);
        }

        try
        {
            var bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, (int)length);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    /// <inheritdoc />
    public Task SetAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var account = Encoding.UTF8.GetBytes(key);
        var secret = Encoding.UTF8.GetBytes(value);

        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, IntPtr.Zero, IntPtr.Zero, out var item);
        if (status == Success)
        {
            status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)secret.Length, secret);
            CFRelease(item);
        }
        else if (status == ItemNotFound)
        {
            status = SecKeychainAddGenericPassword(
                IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, (uint)secret.Length, secret, IntPtr.Zero);
        }

        Trace("write", status);
        return status == Success ? Task.CompletedTask : throw new SecretStoreUnavailableException("write", status);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        var account = Encoding.UTF8.GetBytes(key);
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, IntPtr.Zero, IntPtr.Zero, out var item);
        if (status == Success)
        {
            status = SecKeychainItemDelete(item);
            CFRelease(item);
        }

        Trace("delete", status);
        return status is Success or ItemNotFound ? Task.CompletedTask : throw new SecretStoreUnavailableException("delete", status);
    }

    private static int Find(byte[] account, out uint length, out IntPtr data, out IntPtr item)
        => SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, out length, out data, out item);

    // Diagnosis (US-007 first light): OSStatus of each operation on stderr in Debug builds; never the value.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void Trace(string operation, int status) => Console.Error.WriteLine($"EagleEye keychain {operation}: OSStatus {status}");

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, IntPtr itemRef);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

    [DllImport(SecurityLibrary, EntryPoint = "SecKeychainFindGenericPassword")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray, uint serviceLength, byte[] service, uint accountLength, byte[] account, IntPtr passwordLength, IntPtr passwordData, out IntPtr itemRef);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(IntPtr cf);
}
