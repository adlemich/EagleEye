using EagleEye.ParentApp.Core.Appearance;
using Xunit;

namespace EagleEye.ParentApp.Tests.Appearance;

public sealed class AppleAccentPaletteTests
{
    public static TheoryData<int?, RgbColor> Defined => new()
    {
        { -1, AppleAccentPalette.Graphite },
        { 0, AppleAccentPalette.Red },
        { 1, AppleAccentPalette.Orange },
        { 2, AppleAccentPalette.Yellow },
        { 3, AppleAccentPalette.Green },
        { 4, AppleAccentPalette.Blue },
        { 5, AppleAccentPalette.Purple },
        { 6, AppleAccentPalette.Pink },
        { null, AppleAccentPalette.Blue },
    };

    [Theory]
    [MemberData(nameof(Defined))]
    public void FromAppleAccentColor_DefinedOrAbsent_ReturnsColour(int? value, RgbColor expected)
    {
        Assert.Equal(expected, AppleAccentPalette.FromAppleAccentColor(value));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void FromAppleAccentColor_Undefined_ReturnsNull(int value)
    {
        Assert.Null(AppleAccentPalette.FromAppleAccentColor(value));
    }

    [Fact]
    public void Blue_IsApplesSystemBlue()
    {
        Assert.Equal(new RgbColor(0x00, 0x7A, 0xFF), AppleAccentPalette.Blue);
    }

    [Theory]
    [InlineData(2)] // yellow
    [InlineData(1)] // orange
    [InlineData(4)] // blue: white reaches only about 4.0:1 on #007AFF, below WCAG AA (US-007 DV-4)
    [InlineData(5)] // purple: about 4.1:1
    [InlineData(0)] // red
    public void HeadingBarPalette_LightAccent_GetsDarkText(int value)
    {
        var (_, text) = HeadingBarPalette.For(AppleAccentPalette.FromAppleAccentColor(value));

        Assert.Equal(RgbColor.Black, text);
    }
}
