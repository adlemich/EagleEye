namespace EagleEye.TrayClient.UI;

/// <summary>
/// Extended window styles of the tray client's own popups (US-010 Decision 9, FR-TRAY-032, spike finding D-10). Every popup
/// without a taskbar button (context menu, submenus, overlay) is a tool window, so EagleEye's own app detection (ADR-011 §4,
/// <c>AppWindowRule</c>) never counts it as an app. Pure constants.
/// </summary>
internal static class TrayWindowStyles
{
    /// <summary>Always on top.</summary>
    public const int Topmost = 0x8;

    /// <summary>Mouse input passes through (with <see cref="Layered"/>).</summary>
    public const int Transparent = 0x20;

    /// <summary>Tool window: no taskbar button, not in Alt+Tab, not an "app" for ADR-011 §4.</summary>
    public const int ToolWindow = 0x80;

    /// <summary>Forces a taskbar button; never used by the tray's popups.</summary>
    public const int AppWindow = 0x40000;

    /// <summary>Layered window (per-pixel alpha with <c>UpdateLayeredWindow</c>).</summary>
    public const int Layered = 0x80000;

    /// <summary>Never activated by a click.</summary>
    public const int NoActivate = 0x8000000;

    /// <summary>Added to the context menu and every submenu (spike S-17).</summary>
    public const int MenuExStyle = ToolWindow;

    /// <summary>Added to the overlay window (spike S-1).</summary>
    public const int OverlayExStyle = Layered | Transparent | Topmost | NoActivate | ToolWindow;
}
