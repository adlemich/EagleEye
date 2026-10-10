#if DEBUG
using System.Globalization;

namespace EagleEye.Service;

/// <summary>
/// <b>Debug builds only</b> (ADR-011 T-13): switches for DEV's console-mode smoke checks without admin rights.
/// Release builds do not contain this class; the installers package Release.
/// </summary>
public static class DevSettings
{
    /// <summary>SID of an account that is watched with the Debug launcher (agent as child process in the own session);
    /// the account is also treated as a standard account, so it can be ticked in the parent app.</summary>
    public const string WatchSidVariable = "EAGLEEYE_DEV_WATCH_SID";

    /// <summary>Offset added to both ports (tray and parent endpoint), e.g. 10000 → 15080 and 15443, so a console
    /// service can run next to an installed one.</summary>
    public const string PortOffsetVariable = "EAGLEEYE_DEV_PORT_OFFSET";

    /// <summary>Largest accepted port offset.</summary>
    public const int MaxPortOffset = 50_000;

    /// <summary>The watched SID, or null.</summary>
    public static string? WatchSid()
    {
        var value = Environment.GetEnvironmentVariable(WatchSidVariable)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>The port offset (0 if unset or invalid).</summary>
    public static int PortOffset()
    {
        var value = Environment.GetEnvironmentVariable(PortOffsetVariable);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) && offset <= MaxPortOffset ? offset : 0;
    }
}
#endif
