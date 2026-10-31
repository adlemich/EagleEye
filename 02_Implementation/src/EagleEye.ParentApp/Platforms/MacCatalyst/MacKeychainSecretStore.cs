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
#if DEBUG
    /// <summary>Keychain service of Debug builds: kept apart from the installed app (US-007 Step 1 review R-2).</summary>
    public const string Service = "com.adlemich.eagleeye.debug";
#else
    /// <summary>Keychain service of all EagleEye items (= bundle ID).</summary>
    public const string Service = "com.adlemich.eagleeye";
#endif

    /// <summary>Name of the item in Keychain Access (US-007 Step 1 review R-5).</summary>
    public const string Label = "EagleEye Parent App";

    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private const uint LabelAttributeTag = 0x6C61626C; // kSecLabelItemAttr ('labl')

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
                IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, (uint)secret.Length, secret, out var added);
            if (status == Success)
            {
                SetLabel(added);
                CFRelease(added);
            }
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

    /// <summary>Sets the visible name; presentation only, so a failure is traced and ignored (R-5).</summary>
    private static void SetLabel(IntPtr item)
    {
        var label = Encoding.UTF8.GetBytes(Label);
        var labelData = Marshal.AllocHGlobal(label.Length);
        var attribute = Marshal.AllocHGlobal(Marshal.SizeOf<KeychainAttribute>());
        try
        {
            Marshal.Copy(label, 0, labelData, label.Length);
            Marshal.StructureToPtr(new KeychainAttribute(LabelAttributeTag, (uint)label.Length, labelData), attribute, false);
            var list = new KeychainAttributeList(1, attribute);
            Trace("label", SecKeychainItemModifyAttributesAndData(item, ref list, 0, null));
        }
        finally
        {
            Marshal.FreeHGlobal(attribute);
            Marshal.FreeHGlobal(labelData);
        }
    }

    private static int Find(byte[] account, out uint length, out IntPtr data, out IntPtr item)
        => SecKeychainFindGenericPassword(
            IntPtr.Zero, (uint)ServiceBytes.Length, ServiceBytes, (uint)account.Length, account, out length, out data, out item);

    // Diagnosis (US-007 first light): OSStatus of each operation on stderr in Debug builds; never the value.
    [System.Diagnostics.Conditional("DEBUG")]
    private static void Trace(string operation, int status) => Console.Error.WriteLine($"EagleEye keychain {operation}: OSStatus {status}");

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, out IntPtr itemRef);

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

    [DllImport(SecurityLibrary, EntryPoint = "SecKeychainItemModifyAttributesAndData")]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, ref KeychainAttributeList attrList, uint length, byte[]? data);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct KeychainAttribute(uint Tag, uint Length, IntPtr Data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct KeychainAttributeList(uint Count, IntPtr Attributes);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(IntPtr cf);
}
