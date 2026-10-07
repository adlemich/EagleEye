using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EagleEye.Service.UserAccounts;

/// <summary>
/// Reads the local SAM accounts with the NetApi32 functions (US-003 plan): <c>NetUserEnum</c>
/// (normal accounts), <c>NetUserGetInfo</c> level 23 (full name, flags, SID) and
/// <c>NetUserGetLocalGroups</c> with indirect membership (admin check against the group with the
/// well-known SID S-1-5-32-544, whose name is resolved once, so "Administratoren" works). Accounts
/// linked to a Microsoft account are local SAM accounts and are returned like any other. Thin
/// interop, not unit-tested; verified manually.
/// </summary>
public sealed partial class NetApiLocalAccountSource : ILocalAccountSource
{
    private const int NerrSuccess = 0;
    private const int ErrorMoreData = 234;
    private const int NerrUserNotFound = 2221;
    private const int FilterNormalAccount = 0x0002;
    private const int UfAccountDisable = 0x0002;
    private const int LgIncludeIndirect = 0x0001;
    private const int MaxPreferredLength = -1;

    private readonly Lazy<string> _administratorsGroupName = new(ResolveAdministratorsGroupName);

    /// <inheritdoc />
    public IReadOnlyList<LocalAccountInfo> GetAccounts()
    {
        var accounts = new List<LocalAccountInfo>();
        foreach (var userName in EnumerateUserNames())
        {
            if (TryGetAccount(userName) is { } account)
            {
                accounts.Add(account);
            }
        }

        return accounts;
    }

    private static List<string> EnumerateUserNames()
    {
        var names = new List<string>();
        var resumeHandle = 0;
        int status;
        do
        {
            status = NetUserEnum(null, 0, FilterNormalAccount, out var buffer, MaxPreferredLength, out var read, out _, ref resumeHandle);
            try
            {
                if (status is not NerrSuccess and not ErrorMoreData)
                {
                    throw new Win32Exception(status);
                }

                for (var i = 0; i < read; i++)
                {
                    var entry = Marshal.ReadIntPtr(buffer, i * IntPtr.Size);
                    names.Add(Marshal.PtrToStringUni(entry) ?? string.Empty);
                }
            }
            finally
            {
                Free(buffer);
            }
        }
        while (status == ErrorMoreData);

        return names;
    }

    private LocalAccountInfo? TryGetAccount(string userName)
    {
        var status = NetUserGetInfo(null, userName, 23, out var buffer);
        try
        {
            if (status == NerrUserNotFound)
            {
                // Deleted between the enumeration and this call.
                return null;
            }

            if (status != NerrSuccess)
            {
                throw new Win32Exception(status);
            }

            var info = Marshal.PtrToStructure<UserInfo23>(buffer);
            var fullName = Marshal.PtrToStringUni(info.FullName);
            return new LocalAccountInfo(
                new SecurityIdentifier(info.UserSid).Value,
                Marshal.PtrToStringUni(info.Name) ?? userName,
                string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                (info.Flags & UfAccountDisable) != 0,
                IsAdministrator(userName));
        }
        finally
        {
            Free(buffer);
        }
    }

    private bool IsAdministrator(string userName)
    {
        var status = NetUserGetLocalGroups(null, userName, 0, LgIncludeIndirect, out var buffer, MaxPreferredLength, out var read, out _);
        try
        {
            if (status != NerrSuccess)
            {
                throw new Win32Exception(status);
            }

            for (var i = 0; i < read; i++)
            {
                var name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, i * IntPtr.Size));
                if (string.Equals(name, _administratorsGroupName.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Free(buffer);
        }
    }

    private static string ResolveAdministratorsGroupName()
    {
        var account = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)).Value;
        var separator = account.LastIndexOf('\\');
        return separator < 0 ? account : account[(separator + 1)..];
    }

    private static void Free(IntPtr buffer)
    {
        if (buffer != IntPtr.Zero)
        {
            _ = NetApiBufferFree(buffer);
        }
    }

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NetUserEnum(
        string? serverName, int level, int filter, out IntPtr buffer, int preferredMaxLength,
        out int entriesRead, out int totalEntries, ref int resumeHandle);

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NetUserGetInfo(string? serverName, string userName, int level, out IntPtr buffer);

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NetUserGetLocalGroups(
        string? serverName, string userName, int level, int flags, out IntPtr buffer, int preferredMaxLength,
        out int entriesRead, out int totalEntries);

    [LibraryImport("netapi32.dll")]
    private static partial int NetApiBufferFree(IntPtr buffer);

    /// <summary><c>USER_INFO_23</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UserInfo23
    {
        public readonly IntPtr Name;
        public readonly IntPtr FullName;
        public readonly IntPtr Comment;
        public readonly int Flags;
        public readonly IntPtr UserSid;
    }
}
