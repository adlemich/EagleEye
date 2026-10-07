namespace EagleEye.Service.SessionAgent;

/// <summary>The window metadata the session agent reads for one top-level window (no title, ADR-011 §7 item 8).</summary>
/// <param name="Handle">The window handle.</param>
/// <param name="ProcessId">The process that owns the window.</param>
/// <param name="ClassName">The window class (at most 256 characters).</param>
/// <param name="IsVisible"><c>IsWindowVisible</c> (also true for minimised windows).</param>
/// <param name="Width">Width of the window rectangle.</param>
/// <param name="Height">Height of the window rectangle.</param>
/// <param name="HasOwner">The window has an owner window.</param>
/// <param name="ExStyle">The extended window style.</param>
/// <param name="Cloaked">The <c>DWMWA_CLOAKED</c> flags (0 = not cloaked).</param>
/// <param name="HostedProcessId">For <c>ApplicationFrameWindow</c>: the process of its <c>CoreWindow</c> child, if any.</param>
/// <param name="ProcessName">The file name of the owning process (e.g. <c>explorer.exe</c>), or empty if unknown.</param>
public sealed record WindowInfo(
    nint Handle,
    int ProcessId,
    string ClassName,
    bool IsVisible,
    int Width,
    int Height,
    bool HasOwner,
    long ExStyle,
    int Cloaked,
    int? HostedProcessId,
    string ProcessName);
