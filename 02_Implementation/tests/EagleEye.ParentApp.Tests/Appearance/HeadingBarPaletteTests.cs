using EagleEye.ParentApp.Core.Appearance;
using Xunit;

namespace EagleEye.ParentApp.Tests.Appearance;

public sealed class HeadingBarPaletteTests
{
    [Fact]
    public void For_NoAccent_UsesAppPrimaryWithWhiteText()
    {
        var colors = HeadingBarPalette.For(null);

        Assert.Equal((new RgbColor(0x1E, 0x7B, 0x3A), RgbColor.White), colors);
    }

    [Fact]
    public void For_WindowsDefaultBlue_UsesAccentWithWhiteText()
    {
        var blue = new RgbColor(0x00, 0x78, 0xD4);

        var colors = HeadingBarPalette.For(blue);

        Assert.Equal((blue, RgbColor.White), colors);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00)] // black
    [InlineData(0x1E, 0x7B, 0x3A)] // app green
    [InlineData(0x88, 0x17, 0x98)] // purple
    [InlineData(0xC3, 0x00, 0x52)] // red
    public void TextFor_DarkAccent_IsWhite(byte r, byte g, byte b)
    {
        Assert.Equal(RgbColor.White, HeadingBarPalette.TextFor(new RgbColor(r, g, b)));
    }

    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF)] // white
    [InlineData(0xFF, 0xB9, 0x00)] // Windows yellow
    [InlineData(0x00, 0xCC, 0x6A)] // light green
    [InlineData(0x99, 0xEB, 0xFF)] // light blue
    public void TextFor_LightAccent_IsBlack(byte r, byte g, byte b)
    {
        Assert.Equal(RgbColor.Black, HeadingBarPalette.TextFor(new RgbColor(r, g, b)));
    }

    [Fact]
    public void TextFor_JustBelowMinimumWithWhite_IsBlackWhichIsMoreReadable()
    {
        // #787878: white is just below 4.5:1, black is better.
        var background = new RgbColor(0x78, 0x78, 0x78);
        var onWhite = HeadingBarPalette.ContrastRatio(RgbColor.White, background);
        var onBlack = HeadingBarPalette.ContrastRatio(RgbColor.Black, background);

        var whiteTooLow = onWhite < HeadingBarPalette.MinimumTextContrast;
        var blackBetter = onBlack > onWhite;

        Assert.Equal((true, true, RgbColor.Black), (whiteTooLow, blackBetter, HeadingBarPalette.TextFor(background)));
    }

    [Fact]
    public void TextFor_JustAboveMinimumWithWhite_IsWhite()
    {
        // #767676 is the classic 4.54:1 grey.
        Assert.Equal(RgbColor.White, HeadingBarPalette.TextFor(new RgbColor(0x76, 0x76, 0x76)));
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00, 0.0)]
    [InlineData(0xFF, 0xFF, 0xFF, 1.0)]
    [InlineData(0x08, 0x08, 0x08, 0.00243)] // linear segment
    public void RelativeLuminance_KnownValues(byte r, byte g, byte b, double expected)
    {
        Assert.Equal(expected, HeadingBarPalette.RelativeLuminance(new RgbColor(r, g, b)), 5);
    }

    [Fact]
    public void ContrastRatio_BlackOnWhite_Is21AndSymmetric()
    {
        var ratios = (Math.Round(HeadingBarPalette.ContrastRatio(RgbColor.Black, RgbColor.White), 6), Math.Round(HeadingBarPalette.ContrastRatio(RgbColor.White, RgbColor.Black), 6));

        Assert.Equal((21.0, 21.0), ratios);
    }
}
