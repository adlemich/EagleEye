using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// Draws the pill into a 32-bpp ARGB bitmap (US-010 Decision 5, spike S-11, S-12): dark rounded background with the chosen
/// alpha, the top line in "Segoe UI Semibold" 16 pt, the others at 0.6 ×, each in its level's colour, left-aligned; the width
/// comes from a digit-normalised template so it does not jitter every second. In a bottom corner the lines are drawn in
/// reverse order, so the top line stays at the corner (AC-4). Fonts are cached per DPI and the bitmap is reused while its
/// size stays the same (TI-11). Verified manually.
/// </summary>
internal sealed class PillRenderer : IDisposable
{
    private const float MainFontPt = 16f;
    private const float SmallFactor = 0.6f;
    private const float PointsPerInch = 72f;
    private const float LogicalDpi = 96f;
    private const float PaddingX = 14f;
    private const float PaddingY = 6f;
    private const float LineGap = 2f;
    private const string PreferredFont = "Segoe UI Semibold";
    private const string FallbackFont = "Segoe UI";

    private static readonly Color PillColor = Color.FromArgb(24, 24, 24);
    private static readonly Color NormalColor = Color.White;
    private static readonly Color EarlyColor = Color.FromArgb(255, 214, 0);
    private static readonly Color FinalColor = Color.FromArgb(255, 70, 70);

    private readonly Bitmap _probe = new(1, 1);
    private readonly Graphics _measure;
    private int _dpi;
    private Font? _mainFont;
    private Font? _smallFont;
    private Bitmap? _bitmap;

    /// <summary>Creates the renderer.</summary>
    public PillRenderer()
    {
        _measure = Graphics.FromImage(_probe);
        _measure.TextRenderingHint = TextRenderingHint.AntiAlias;
    }

    /// <summary>Renders the pill; the returned bitmap belongs to the renderer and is valid until the next call.</summary>
    public Bitmap Render(PillLayout layout, byte backgroundAlpha, int dpi, bool bottomCorner)
    {
        ArgumentNullException.ThrowIfNull(layout);
        EnsureFonts(dpi);
        var scale = dpi / LogicalDpi;
        float padX = PaddingX * scale, padY = PaddingY * scale, gap = LineGap * scale;
        var format = StringFormat.GenericTypographic;
        var measured = layout.Lines.Select(line => (Line: line, Font: line.IsTop ? _mainFont! : _smallFont!)).Select(l => (l.Line, l.Font, Size: Measure(l.Line.Text, l.Font, format))).ToList();
        var width = (int)Math.Ceiling(measured.Max(m => m.Size.Width) + (2 * padX));
        var height = (int)Math.Ceiling(measured.Sum(m => m.Size.Height) + (gap * (measured.Count - 1)) + (2 * padY));
        var bitmap = Canvas(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);
        var radius = Math.Min(height, measured[0].Size.Height + (2 * padY)) / 2f;
        using (var path = RoundedRect(new RectangleF(0, 0, width, height), radius))
        using (var background = new SolidBrush(Color.FromArgb(backgroundAlpha, PillColor)))
        {
            g.FillPath(background, path);
        }

        var y = padY;
        foreach (var (line, font, size) in bottomCorner ? Enumerable.Reverse(measured) : measured)
        {
            using var brush = new SolidBrush(ColorOf(line.Level));
            g.DrawString(line.Text, font, brush, padX, y, format);
            y += size.Height + gap;
        }

        return bitmap;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _bitmap?.Dispose();
        _mainFont?.Dispose();
        _smallFont?.Dispose();
        _measure.Dispose();
        _probe.Dispose();
    }

    private static Color ColorOf(OverlayLevel level) => level switch
    {
        OverlayLevel.Normal => NormalColor,
        OverlayLevel.Early => EarlyColor,
        _ => FinalColor,
    };

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
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
        var font = new Font(PreferredFont, px, FontStyle.Regular, GraphicsUnit.Pixel);
        if (font.Name == PreferredFont)
        {
            return font;
        }

        font.Dispose();
        return new Font(FallbackFont, px, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    /// <summary>The larger of the real size and the size with every ASCII digit as "0" (spike S-12).</summary>
    private SizeF Measure(string text, Font font, StringFormat format)
    {
        var template = new string([.. text.Select(c => char.IsAsciiDigit(c) ? '0' : c)]);
        var templateSize = _measure.MeasureString(template, font, PointF.Empty, format);
        var actual = _measure.MeasureString(text, font, PointF.Empty, format);
        return new SizeF(Math.Max(templateSize.Width, actual.Width), actual.Height);
    }

    private void EnsureFonts(int dpi)
    {
        if (dpi == _dpi && _mainFont is not null)
        {
            return;
        }

        _mainFont?.Dispose();
        _smallFont?.Dispose();
        var mainPx = MainFontPt * dpi / PointsPerInch;
        _mainFont = CreateFont(mainPx);
        _smallFont = CreateFont(mainPx * SmallFactor);
        _dpi = dpi;
    }

    private Bitmap Canvas(int width, int height)
    {
        if (_bitmap is null || _bitmap.Width != width || _bitmap.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        }

        return _bitmap;
    }
}
