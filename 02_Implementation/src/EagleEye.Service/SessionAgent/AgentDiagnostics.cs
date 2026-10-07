using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// The agent's diagnostic line at start (ADR-011 §7 item 10): token user, integrity level, enabled privileges and
/// whether the token is write-restricted. The service logs it at Information, so the hardening is visible in the
/// service log. Thin Win32, verified manually.
/// </summary>
public static unsafe partial class AgentDiagnostics
{
    /// <summary>Prefix of the diagnostic line, recognised by the supervisor.</summary>
    public const string Prefix = "diagnostics:";

    private const int TokenPrivileges = 3;
    private const int TokenIntegrityLevel = 25;
    private const uint PrivilegeEnabled = 0x2;

    /// <summary>Describes the agent's own token.</summary>
    public static string Describe()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var token = identity.AccessToken.DangerousGetHandle();
        var user = identity.User is { } sid ? $"{identity.Name} ({sid.Value})" : identity.Name;
        return $"{Prefix} user {user}, integrity {Integrity(token)}, privileges {Privileges(token)}, " +
               $"write-restricted {(IsTokenRestricted(token) ? "yes" : "no")}";
    }

    private static string Integrity(nint token)
    {
        var buffer = Query(token, TokenIntegrityLevel);
        if (buffer is null)
        {
            return "unknown";
        }

        fixed (byte* data = buffer)
        {
            var sid = new SecurityIdentifier(*(nint*)data);
            var rid = int.Parse(sid.Value[(sid.Value.LastIndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            return rid switch
            {
                0x4000 => "System",
                0x3000 => "High",
                0x2000 => "Medium",
                0x1000 => "Low",
                0 => "Untrusted",
                _ => $"0x{rid:X}",
            };
        }
    }

    private static string Privileges(nint token)
    {
        var buffer = Query(token, TokenPrivileges);
        if (buffer is null)
        {
            return "unknown";
        }

        var names = new List<string>();
        fixed (byte* data = buffer)
        {
            var count = *(uint*)data;
            for (var i = 0; i < count; i++)
            {
                var entry = data + sizeof(uint) + (i * 12);
                if ((*(uint*)(entry + 8) & PrivilegeEnabled) != 0)
                {
                    names.Add(PrivilegeName((long*)entry));
                }
            }
        }

        return names.Count == 0 ? "none" : string.Join(' ', names);
    }

    private static string PrivilegeName(long* luid)
    {
        var buffer = stackalloc char[128];
        var length = 128u;
        return LookupPrivilegeNameW(null, luid, buffer, ref length) ? new string(buffer, 0, (int)length) : "?";
    }

    private static byte[]? Query(nint token, int informationClass)
    {
        _ = GetTokenInformation(token, informationClass, null, 0, out var needed);
        if (needed == 0)
        {
            return null;
        }

        var buffer = new byte[needed];
        fixed (byte* data = buffer)
        {
            return GetTokenInformation(token, informationClass, data, needed, out _) ? buffer : null;
        }
    }

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetTokenInformation(nint token, int informationClass, byte* information, uint length, out uint returnLength);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeNameW(string? systemName, long* luid, char* name, ref uint length);

    [LibraryImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsTokenRestricted(nint token);

    /// <summary>Encoding of the agent's streams (UTF-8 without BOM).</summary>
    internal static readonly Encoding StreamEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
