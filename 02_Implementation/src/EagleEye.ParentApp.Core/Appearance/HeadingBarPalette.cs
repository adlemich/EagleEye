namespace EagleEye.ParentApp.Core.Appearance;

/// <summary>
/// Colours of the heading bar (US-004 ISSUE-007): the platform accent colour as background, or the
/// app's primary colour where the platform has none, and a text colour that stays readable on it.
/// </summary>
public static class HeadingBarPalette
{
    /// <summary>Minimum contrast ratio for normal text (WCAG 2.x, level AA).</summary>
    public const double MinimumTextContrast = 4.5;

    // WCAG 2.x relative luminance: sRGB linearization and channel weights.
    private const double SrgbLinearThreshold = 0.04045;
    private const double SrgbLinearDivisor = 12.92;
    private const double SrgbOffset = 0.055;
    private const double SrgbScale = 1.055;
    private const double SrgbExponent = 2.4;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double ContrastFlare = 0.05;

    /// <summary>The app's primary colour (Colors.xaml <c>AccentLight</c>, Android <c>colorPrimary</c>).</summary>
    public static RgbColor FallbackBackground { get; } = new(0x1E, 0x7B, 0x3A);

    /// <summary>Background and text colour of the bar for the given platform accent colour.</summary>
    /// <param name="accent">The platform accent colour, or <c>null</c> when the platform has none.</param>
    public static (RgbColor Background, RgbColor Text) For(RgbColor? accent)
    {
        var background = accent ?? FallbackBackground;
        return (background, TextFor(background));
    }

    /// <summary>
    /// White text (as in Michael's mockup) when it reaches <see cref="MinimumTextContrast"/>;
    /// otherwise black (light accent colours such as yellow). Below 4.5 with white, black always
    /// has the higher contrast (at least 4.6).
    /// </summary>
    public static RgbColor TextFor(RgbColor background) =>
        ContrastRatio(RgbColor.White, background) >= MinimumTextContrast ? RgbColor.White : RgbColor.Black;

    /// <summary>WCAG contrast ratio between two colours (1 to 21).</summary>
    public static double ContrastRatio(RgbColor first, RgbColor second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + ContrastFlare) / (Math.Min(a, b) + ContrastFlare);
    }

    /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
    public static double RelativeLuminance(RgbColor color) =>
        (RedWeight * Linear(color.R)) + (GreenWeight * Linear(color.G)) + (BlueWeight * Linear(color.B));

    private static double Linear(byte channel)
    {
        var value = channel / (double)byte.MaxValue;
        return value <= SrgbLinearThreshold
            ? value / SrgbLinearDivisor
            : Math.Pow((value + SrgbOffset) / SrgbScale, SrgbExponent);
    }
}
