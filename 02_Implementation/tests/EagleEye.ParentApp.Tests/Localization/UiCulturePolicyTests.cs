using System.Globalization;
using EagleEye.ParentApp.Core.Localization;
using Xunit;

namespace EagleEye.ParentApp.Tests.Localization;

public sealed class UiCulturePolicyTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("EN-us")]
    [InlineData("en_DE")]
    [InlineData(" en-AU ")]
    public void ResolveUiCulture_English_ReturnsEn(string language)
    {
        Assert.Equal("en", UiCulturePolicy.ResolveUiCulture(language).Name);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("de-CH")]
    [InlineData("fr-FR")]
    [InlineData("zh-Hans-CN")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("garbage!")]
    [InlineData("eng")]
    public void ResolveUiCulture_NotEnglish_ReturnsDe(string? language)
    {
        Assert.Equal("de", UiCulturePolicy.ResolveUiCulture(language).Name);
    }

    [Theory]
    [InlineData("de", "DE", "de-DE")]
    [InlineData("en", "US", "en-US")]
    [InlineData("en", "DE", "en-DE")]
    [InlineData("de", " AT ", "de-AT")]
    public void ResolveFormattingCulture_KnownPair_ReturnsLanguageAndRegion(string ui, string region, string expected)
    {
        Assert.Equal(expected, UiCulturePolicy.ResolveFormattingCulture(CultureInfo.GetCultureInfo(ui), region).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("XX")]
    [InlineData("not a region")]
    public void ResolveFormattingCulture_MissingOrUnknownRegion_ReturnsUiCulture(string? region)
    {
        Assert.Equal("de", UiCulturePolicy.ResolveFormattingCulture(CultureInfo.GetCultureInfo("de"), region).Name);
    }

    [Fact]
    public void ResolveFormattingCulture_NullUiCulture_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UiCulturePolicy.ResolveFormattingCulture(null!, "DE"));
    }

    [Theory]
    [InlineData("de", "DE", "20.10.2026")]
    [InlineData("en", "US", "10/20/2026")]
    public void ResolveFormattingCulture_ShortDate_FollowsRegion(string ui, string region, string expected)
    {
        var culture = UiCulturePolicy.ResolveFormattingCulture(CultureInfo.GetCultureInfo(ui), region);

        Assert.Equal(expected, new DateTime(2026, 10, 20).ToString("d", culture));
    }
}
