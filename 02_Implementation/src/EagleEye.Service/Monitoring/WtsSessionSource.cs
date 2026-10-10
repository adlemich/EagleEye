using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Reads the sessions with the WTS API (ADR-012 §2): <c>WTSEnumerateSessions</c>, then per session the user and
/// domain name (<c>WTSUserName</c>, <c>WTSDomainName</c>, translated to the SID) and the lock flag
/// (<c>WTSSessionInfoEx</c>). Works as SYSTEM and, for the own session, unelevated (console mode). Thin Win32,
/// verified manually.
/// </summary>
public sealed partial class WtsSessionSource(ILogger<WtsSessionSource> logger) : ISessionSource
{
    private const int WtsUserName = 5;
    private const int WtsDomainName = 7;
    private const int WtsSessionInfoEx = 25;
    private const int SessionFlagsOffset = 16;
    private const int SessionStateLock = 0;

    private readonly ConcurrentDictionary<string, string?> _sids = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<SessionInfo> GetSessions()
    {
        if (!WTSEnumerateSessionsW(0, 0, 1, out var buffer, out var count))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }

        var sessions = new List<SessionInfo>((int)count);
        try
        {
            var size = Marshal.SizeOf<WtsSessionInfo>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<WtsSessionInfo>(buffer + (i * size));
                sessions.Add(new SessionInfo((int)info.SessionId, UserSid(info.SessionId), (SessionConnectState)info.State, IsLocked(info.SessionId)));
            }
        }
        finally
        {
            WTSFreeMemory(buffer);
        }

        return sessions;
    }

    private string? UserSid(uint sessionId)
    {
        var user = QueryString(sessionId, WtsUserName);
        if (string.IsNullOrEmpty(user))
        {
            return null;
        }

        var account = $"{QueryString(sessionId, WtsDomainName)}\\{user}";
        return _sids.GetOrAdd(account, Translate);
    }

    private string? Translate(string account)
    {
        try
        {
            return new NTAccount(account).Translate(typeof(SecurityIdentifier)).Value;
        }
        catch (IdentityNotMappedException ex)
        {
            logger.LogWarning(ex, "The session user {Account} could not be mapped to a SID.", account);
            return null;
        }
    }

    private static bool IsLocked(uint sessionId)
    {
        if (!WTSQuerySessionInformationW(0, sessionId, WtsSessionInfoEx, out var buffer, out var bytes))
        {
            return false;
        }

        try
        {
            return bytes > SessionFlagsOffset + sizeof(int) && Marshal.ReadInt32(buffer, SessionFlagsOffset) == SessionStateLock;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string? QueryString(uint sessionId, int infoClass)
    {
        if (!WTSQuerySessionInformationW(0, sessionId, infoClass, out var buffer, out _))
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsSessionInfo
    {
        public uint SessionId;
        public nint WinStationName;
        public int State;
    }

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSEnumerateSessionsW(nint server, uint reserved, uint version, out nint sessionInfo, out uint count);

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformationW(nint server, uint sessionId, int infoClass, out nint buffer, out uint bytesReturned);

    [LibraryImport("wtsapi32.dll")]
    private static partial void WTSFreeMemory(nint memory);
}
