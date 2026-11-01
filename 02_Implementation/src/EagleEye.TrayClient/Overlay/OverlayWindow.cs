using EagleEye.TrayClient.UI;

namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// The overlay window (US-010 Decision 4, ADR-022 §1, spike S-1 to S-4): borderless, layered with per-pixel alpha,
/// click-through, top-most, never activated, a tool window (no taskbar button, not in Alt+Tab, not an "app" for ADR-011 §4).
/// Created once at tray start, kept hidden while there is nothing to show; never cancels closing (sign-out, AC-25).
/// Verified manually.
/// </summary>
internal sealed unsafe class OverlayWindow : Form
{
    private static readonly Point OffScreen = new(-32000, -32000);

    /// <summary>Creates the window off-screen at 1 × 1.</summary>
    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Text = "EagleEye";
        Location = OffScreen;
        Size = new Size(1, 1);
    }

    /// <summary>Raised on <c>WM_DPICHANGED</c> (the controller re-renders; WinForms' own rescaling is suppressed).</summary>
    public event EventHandler? DpiChangedMessage;

    /// <inheritdoc />
    protected override bool ShowWithoutActivation => true;

    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= TrayWindowStyles.OverlayExStyle;
            return cp;
        }
    }

    /// <summary>Shows <paramref name="bitmap"/> (32-bpp ARGB) at <paramref name="location"/> (physical pixels) with per-pixel alpha.</summary>
    public void Present(Bitmap bitmap, Point location)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var screenDc = OverlayNative.GetDC(0);
        var memDc = OverlayNative.CreateCompatibleDC(screenDc);
        var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var old = OverlayNative.SelectObject(memDc, hBitmap);
        try
        {
            var dst = new OverlayNative.POINT { X = location.X, Y = location.Y };
            var size = new OverlayNative.SIZE { Cx = bitmap.Width, Cy = bitmap.Height };
            var src = default(OverlayNative.POINT);
            var blend = new OverlayNative.BLENDFUNCTION
            {
                BlendOp = OverlayNative.AC_SRC_OVER,
                SourceConstantAlpha = byte.MaxValue,
                AlphaFormat = OverlayNative.AC_SRC_ALPHA,
            };
            _ = OverlayNative.UpdateLayeredWindow(Handle, screenDc, &dst, &size, memDc, &src, 0, &blend, OverlayNative.ULW_ALPHA);
        }
        finally
        {
            OverlayNative.SelectObject(memDc, old);
            OverlayNative.DeleteObject(hBitmap);
            OverlayNative.DeleteDC(memDc);
            _ = OverlayNative.ReleaseDC(0, screenDc);
        }
    }

    /// <summary>Re-asserts the top-most position without activating (ADR-022 §2).</summary>
    public void Reassert() => OverlayNative.SetWindowPos(
        Handle, OverlayNative.HWND_TOPMOST, 0, 0, 0, 0,
        OverlayNative.SWP_NOMOVE | OverlayNative.SWP_NOSIZE | OverlayNative.SWP_NOACTIVATE | OverlayNative.SWP_NOOWNERZORDER);

    /// <summary>Shows the window without activating it.</summary>
    public void ShowNoActivate() => OverlayNative.ShowWindow(Handle, OverlayNative.SW_SHOWNOACTIVATE);

    /// <summary>Hides the window.</summary>
    public void HideOverlay() => OverlayNative.ShowWindow(Handle, OverlayNative.SW_HIDE);

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case OverlayNative.WM_MOUSEACTIVATE:
                m.Result = OverlayNative.MA_NOACTIVATE;
                return;
            case OverlayNative.WM_NCHITTEST:
                m.Result = OverlayNative.HTTRANSPARENT;
                return;
            case OverlayNative.WM_DPICHANGED:
                DpiChangedMessage?.Invoke(this, EventArgs.Empty);
                m.Result = 0;
                return;
            default:
                base.WndProc(ref m);
                return;
        }
    }

    /// <inheritdoc />
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Per-pixel alpha: the content comes from UpdateLayeredWindow only.
    }
}
