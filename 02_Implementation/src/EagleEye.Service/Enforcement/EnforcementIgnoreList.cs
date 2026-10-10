namespace EagleEye.Service.Enforcement;

/// <summary>
/// The shipped enforcement ignore list (ADR-013 §6, ADR-005 Tier 1 for US-005): Windows programs that are never
/// blocked, closed or terminated. Matched by <b>full path</b> under <c>%SystemRoot%</c>, never by name alone
/// (ADR-011 T-10): the shell <c>explorer.exe</c> in <c>%SystemRoot%</c>, the listed programs in
/// <c>%SystemRoot%\System32</c>, and the shell hosts that Windows ships in package folders below
/// <c>%SystemRoot%\SystemApps</c> (the folder names change between Windows builds). Task Manager and the Settings app
/// are deliberately not listed (OQ-3); nor are the accessibility tools (Q-9).
/// </summary>
public sealed class EnforcementIgnoreList
{
    /// <summary>Programs directly in <c>%SystemRoot%\System32</c>.</summary>
    public static readonly IReadOnlyList<string> System32Programs =
    [
        "ApplicationFrameHost.exe", "dwm.exe", "csrss.exe", "winlogon.exe", "sihost.exe", "ctfmon.exe", "conhost.exe",
        "fontdrvhost.exe", "taskhostw.exe", "svchost.exe", "dllhost.exe", "RuntimeBroker.exe", "LogonUI.exe", "consent.exe",
        "WerFault.exe", "smartscreen.exe", "SecurityHealthSystray.exe",
    ];

    /// <summary>Shell hosts below <c>%SystemRoot%\SystemApps</c>.</summary>
    public static readonly IReadOnlyList<string> SystemAppsPrograms =
    [
        "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "SearchHost.exe", "TextInputHost.exe", "LockApp.exe",
    ];

    private readonly HashSet<string> _exact;
    private readonly string _systemApps;

    /// <summary>Creates the list for the PC's Windows folder.</summary>
    public EnforcementIgnoreList(string systemRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemRoot);
        var system32 = Path.Combine(systemRoot, "System32");
        _exact = new HashSet<string>(System32Programs.Select(p => Path.Combine(system32, p)), StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(systemRoot, "explorer.exe"),
        };
        _systemApps = Path.TrimEndingDirectorySeparator(Path.Combine(systemRoot, "SystemApps")) + Path.DirectorySeparatorChar;
    }

    /// <summary>Whether the program is never blocked or ended.</summary>
    public bool IsIgnored(string? programPath)
    {
        if (string.IsNullOrWhiteSpace(programPath) || programPath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return _exact.Contains(programPath)
            || (programPath.StartsWith(_systemApps, StringComparison.OrdinalIgnoreCase)
                && SystemAppsPrograms.Contains(Path.GetFileName(programPath), StringComparer.OrdinalIgnoreCase));
    }
}
