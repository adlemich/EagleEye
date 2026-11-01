namespace EagleEye.TrayClient.UI;

/// <summary>
/// The tray context menu as a tool window (US-010 Decision 9, spike S-17). A plain <see cref="ContextMenuStrip"/> popup is a
/// visible, unowned, non-tool top-level window, which EagleEye's <c>AppWindowRule</c> (ADR-011 §4) counts as an app (spike
/// finding D-10). <see cref="TrayWindowStyles.MenuExStyle"/> avoids that.
/// </summary>
internal sealed class ToolWindowContextMenu : ContextMenuStrip
{
    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= TrayWindowStyles.MenuExStyle;
            return cp;
        }
    }
}

/// <summary>A submenu popup as a tool window (same reason as <see cref="ToolWindowContextMenu"/>).</summary>
internal sealed class ToolWindowDropDown : ToolStripDropDownMenu
{
    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= TrayWindowStyles.MenuExStyle;
            return cp;
        }
    }
}
