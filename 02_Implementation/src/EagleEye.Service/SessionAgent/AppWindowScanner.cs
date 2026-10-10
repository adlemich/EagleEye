namespace EagleEye.Service.SessionAgent;

/// <summary>Turns the windows of one scan into the distinct apps of the session (one entry per process and kind).</summary>
public static class AppWindowScanner
{
    /// <summary>Returns the distinct apps, sorted by PID, kind and host PID.</summary>
    public static IReadOnlyList<AgentApp> Scan(IEnumerable<WindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var apps = new HashSet<AgentApp>();
        foreach (var window in windows)
        {
            if (AppWindowRule.Classify(window) is { } app)
            {
                apps.Add(app);
            }
        }

        return [.. apps.OrderBy(a => a.Pid).ThenBy(a => a.Kind).ThenBy(a => a.HostPid)];
    }
}
