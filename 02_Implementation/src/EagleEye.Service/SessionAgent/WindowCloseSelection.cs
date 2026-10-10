namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Pure part of the close command (ADR-013 §4): which windows of a scan get <c>WM_CLOSE</c>. Only windows that the
/// "Apps" rule (<see cref="AppWindowRule"/>) counts as app windows of a target: ordinary and renamed-explorer app
/// windows of the target's process, and for a Store app the <c>ApplicationFrameWindow</c> that hosts it. File
/// Explorer windows are never selected (OQ-3).
/// </summary>
public static class WindowCloseSelection
{
    /// <summary>The windows to close, with the target PID each one belongs to.</summary>
    /// <param name="targetPids">The PIDs of the targets (creation times are verified by the caller).</param>
    /// <param name="windows">The windows of the current scan.</param>
    public static IReadOnlyList<(int Pid, nint Window)> Select(IReadOnlySet<int> targetPids, IEnumerable<WindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(targetPids);
        ArgumentNullException.ThrowIfNull(windows);
        var selected = new List<(int, nint)>();
        foreach (var window in windows)
        {
            if (AppWindowRule.Classify(window) is { Kind: not AgentAppKind.FileExplorer } app && targetPids.Contains(app.Pid))
            {
                selected.Add((app.Pid, window.Handle));
            }
        }

        return selected;
    }
}
