namespace OverlaySpike;

/// <summary>
/// Context menu whose popup window is a tool window. A plain ContextMenuStrip popup is a visible, unowned,
/// non-tool top-level window (exstyle TOPMOST|CONTROLPARENT), which EagleEye's AppWindowRule (ADR-011 §4) counts
/// as an "app" - found in DEV check D-10 with the installed service's own classifier. WS_EX_TOOLWINDOW avoids that.
/// </summary>
internal sealed class ToolWindowContextMenu : ContextMenuStrip
{
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}

/// <summary>Submenu popup with WS_EX_TOOLWINDOW (same reason as <see cref="ToolWindowContextMenu"/>).</summary>
internal sealed class ToolWindowDropDown : ToolStripDropDownMenu
{
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
