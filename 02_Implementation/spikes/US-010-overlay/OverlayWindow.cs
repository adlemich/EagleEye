using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace OverlaySpike;

/// <summary>What the pill shows.</summary>
internal sealed record PillContent(string Main, string? Sub, Color TextColor, byte BackgroundAlpha, float FontPt);

/// <summary>Draws the pill into a 32-bpp ARGB bitmap at the given DPI.</summary>
internal static class PillRenderer
{
    private static readonly Color PillColor = Color.FromArgb(24, 24, 24);
    private static readonly Color SubColor = Color.FromArgb(230, 230, 230);

    /// <summary>Renders the pill. In uniform mode the background is opaque (the window alpha applies to all).</summary>
    public static Bitmap Render(PillContent c, int dpi, bool uniform)
    {
        var scale = dpi / 96f;
        var mainPx = c.FontPt * dpi / 72f;
        using var mainFont = CreateFont(mainPx);
        using var subFont = CreateFont(mainPx * 0.6f);
        var format = StringFormat.GenericTypographic;
        SizeF mainSize, subSize = SizeF.Empty;
        float textWidth;
        using (var probe = new Bitmap(1, 1))
        using (var mg = Graphics.FromImage(probe))
        {
            mg.TextRenderingHint = TextRenderingHint.AntiAlias;
            // Size from a digit-normalised template so the pill width does not jitter every second.
            var template = new string(c.Main.Select(ch => char.IsAsciiDigit(ch) ? '0' : ch).ToArray());
            var templateSize = mg.MeasureString(template, mainFont, PointF.Empty, format);
            var actualSize = mg.MeasureString(c.Main, mainFont, PointF.Empty, format);
            mainSize = new SizeF(Math.Max(templateSize.Width, actualSize.Width), actualSize.Height);
            textWidth = actualSize.Width;
            if (c.Sub is not null)
            {
                subSize = mg.MeasureString(c.Sub, subFont, PointF.Empty, format);
            }
        }

        float padX = 14 * scale, padY = 6 * scale, gap = 2 * scale;
        var contentW = Math.Max(mainSize.Width, subSize.Width);
        var contentH = mainSize.Height + (c.Sub is null ? 0 : gap + subSize.Height);
        var w = (int)Math.Ceiling(contentW + (2 * padX));
        var h = (int)Math.Ceiling(contentH + (2 * padY));
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);
        var radius = Math.Min(h, mainSize.Height + (2 * padY)) / 2f;
        using (var path = RoundedRect(new RectangleF(0, 0, w, h), radius))
        using (var bg = new SolidBrush(Color.FromArgb(uniform ? 255 : c.BackgroundAlpha, PillColor)))
        {
            g.FillPath(bg, path);
        }

        using (var fg = new SolidBrush(c.TextColor))
        {
            g.DrawString(c.Main, mainFont, fg, (w - textWidth) / 2f, padY, format);
        }

        if (c.Sub is not null)
        {
            using var sb = new SolidBrush(SubColor);
            g.DrawString(c.Sub, subFont, sb, (w - subSize.Width) / 2f, padY + mainSize.Height + gap, format);
        }

        return bmp;
    }

    /// <summary>Rounded rectangle path (also used as window region in uniform mode).</summary>
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = Math.Max(1f, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Font CreateFont(float px)
    {
        var font = new Font("Segoe UI Semibold", px, FontStyle.Regular, GraphicsUnit.Pixel);
        return font.Name == "Segoe UI Semibold" ? font : new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
    }
}

/// <summary>
/// The overlay: borderless, layered, click-through, top-most, never activated, tool window (not in the taskbar,
/// not in Alt+Tab, not an "app" for EagleEye's session agent, ADR-011 §4).
/// </summary>
internal sealed unsafe class OverlayWindow : Form
{
    private readonly bool uniform;
    private Bitmap? painted;

    public OverlayWindow(bool uniform)
    {
        this.uniform = uniform;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Text = "OverlaySpike";
        BackColor = Color.FromArgb(24, 24, 24);
        DoubleBuffered = true;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
    }

    /// <summary>Raised on WM_DPICHANGED (the controller redraws; WinForms' own scaling is suppressed).</summary>
    public event EventHandler? DpiMessage;

    public bool IsUniform => uniform;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    /// <summary>Shows the bitmap at the given screen position (physical pixels) with the given window alpha.</summary>
    public void Present(Bitmap bmp, Point location, byte windowAlpha)
    {
        if (uniform)
        {
            PresentUniform(bmp, location, windowAlpha);
        }
        else
        {
            PresentPerPixel(bmp, location);
        }
    }

    /// <summary>Re-asserts the top-most position without activating.</summary>
    public void Reassert() =>
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);

    public void ShowNoActivate() => Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);

    public void HideOverlay() => Native.ShowWindow(Handle, Native.SW_HIDE);

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case Native.WM_MOUSEACTIVATE:
                m.Result = Native.MA_NOACTIVATE;
                return;
            case Native.WM_NCHITTEST:
                m.Result = Native.HTTRANSPARENT;
                return;
            case Native.WM_DPICHANGED:
                DpiMessage?.Invoke(this, EventArgs.Empty);
                m.Result = 0;
                return;
            default:
                base.WndProc(ref m);
                return;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (uniform && painted is not null)
        {
            e.Graphics.DrawImageUnscaled(painted, 0, 0);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (uniform)
        {
            base.OnPaintBackground(e);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            painted?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PresentPerPixel(Bitmap bmp, Point location)
    {
        var screenDc = Native.GetDC(0);
        var memDc = Native.CreateCompatibleDC(screenDc);
        var hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        var old = Native.SelectObject(memDc, hBitmap);
        try
        {
            var dst = new Native.POINT { X = location.X, Y = location.Y };
            var size = new Native.SIZE { Cx = bmp.Width, Cy = bmp.Height };
            var src = default(Native.POINT);
            var blend = new Native.BLENDFUNCTION { BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = Native.AC_SRC_ALPHA };
            _ = Native.UpdateLayeredWindow(Handle, screenDc, &dst, &size, memDc, &src, 0, &blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBitmap);
            Native.DeleteDC(memDc);
            _ = Native.ReleaseDC(0, screenDc);
        }
    }

    private void PresentUniform(Bitmap bmp, Point location, byte windowAlpha)
    {
        var sizeChanged = painted is null || painted.Width != bmp.Width || painted.Height != bmp.Height;
        painted?.Dispose();
        painted = (Bitmap)bmp.Clone();
        _ = Native.SetWindowPos(Handle, 0, location.X, location.Y, bmp.Width, bmp.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        if (sizeChanged)
        {
            using var path = PillRenderer.RoundedRect(new RectangleF(0, 0, bmp.Width, bmp.Height), Math.Min(bmp.Width, bmp.Height) / 2f);
            var oldRegion = Region;
            Region = new Region(path);
            oldRegion?.Dispose();
        }

        _ = Native.SetLayeredWindowAttributes(Handle, 0, windowAlpha, Native.LWA_ALPHA);
        Invalidate();
    }
}
