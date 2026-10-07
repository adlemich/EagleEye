namespace EagleEye.Service.SessionAgent;

/// <summary>How the session agent found an app window (ADR-011 §4).</summary>
public enum AgentAppKind
{
    /// <summary>An ordinary app window of the process <see cref="AgentApp.Pid"/>.</summary>
    Window,

    /// <summary>A File Explorer window (<c>CabinetWClass</c>) of <c>explorer.exe</c> (US-004 AC-4).</summary>
    FileExplorer,

    /// <summary>
    /// Another app-like window of a process named <c>explorer.exe</c>. The service ignores it for the real
    /// <c>%SystemRoot%\explorer.exe</c> (shell windows, AC-4) and counts it as an ordinary window for any other program
    /// with that name, so renaming a program to <c>explorer.exe</c> does not hide it (ADR-011 T-10).
    /// </summary>
    ExplorerWindow,

    /// <summary>
    /// A Store app shown in an <c>ApplicationFrameWindow</c>: <see cref="AgentApp.Pid"/> is the app process
    /// (the <c>CoreWindow</c> child), <see cref="AgentApp.HostPid"/> the frame's process. The service accepts
    /// the app process only if the frame belongs to the real ApplicationFrameHost (ADR-011 §5).
    /// </summary>
    StoreApp,
}

/// <summary>One app reported by the session agent: a process and how it was found. No names or paths.</summary>
/// <param name="Pid">The process ID of the app.</param>
/// <param name="Kind">How the window was classified.</param>
/// <param name="HostPid">For <see cref="AgentAppKind.StoreApp"/>: the process ID of the frame window; otherwise null.</param>
public sealed record AgentApp(int Pid, AgentAppKind Kind, int? HostPid = null);

/// <summary>One report line of the session agent (ADR-011 §3).</summary>
/// <param name="Seq">Sequence number, increasing per agent run.</param>
/// <param name="Truncated">The window scan stopped at the window limit (ADR-011 §7 item 9).</param>
/// <param name="Apps">The apps of the session, sorted by PID.</param>
public sealed record AgentReport(long Seq, bool Truncated, IReadOnlyList<AgentApp> Apps);
