using System.Collections;
using System.Globalization;
using System.Resources;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class TrayTextsTests
{
    public static TheoryData<string, Func<string>, string> GermanTexts => new()
    {
        { "de-DE", () => TrayTexts.TooltipConnected, "EagleEye — Verbunden" },
        { "de-DE", () => TrayTexts.TooltipDisconnected, "EagleEye — Verbindungsfehler" },
        { "de-DE", () => TrayTexts.AboutMenuItem, "App Infos" },
        { "de-DE", () => TrayTexts.AboutTitle, "EagleEye – App Infos" },
        { "de-DE", () => TrayTexts.ServerVersionFormat, "Server-Version: {0}" },
        { "de-DE", () => TrayTexts.VersionUnavailable, "nicht verfügbar" },
        { "de-DE", () => TrayTexts.Ok, "OK" },
    };

    public static TheoryData<string, Func<string>, string> EnglishTexts => new()
    {
        { "en-US", () => TrayTexts.TooltipConnected, "EagleEye — Connected" },
        { "en-US", () => TrayTexts.TooltipDisconnected, "EagleEye — Disconnected" },
        { "en-US", () => TrayTexts.AboutMenuItem, "About" },
        { "en-US", () => TrayTexts.AboutTitle, "About EagleEye" },
        { "en-US", () => TrayTexts.ServerVersionFormat, "Server Version: {0}" },
        { "en-US", () => TrayTexts.VersionUnavailable, "unavailable" },
        { "en-US", () => TrayTexts.Ok, "OK" },
    };

    [Theory]
    [MemberData(nameof(GermanTexts))]
    public void Text_GermanUiCulture_ReturnsGermanText(string culture, Func<string> text, string expected)
    {
        var actual = InCulture(culture, text);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(EnglishTexts))]
    public void Text_EnglishUiCulture_ReturnsEnglishText(string culture, Func<string> text, string expected)
    {
        var actual = InCulture(culture, text);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void Text_OtherUiCulture_FallsBackToGerman(string culture)
    {
        var actual = InCulture(culture, () => TrayTexts.AboutMenuItem);

        Assert.Equal("App Infos", actual);
    }

    [Fact]
    public void Resources_GermanAndEnglish_DefineTheSameKeys()
    {
        var germanKeys = GetKeys(CultureInfo.InvariantCulture);
        var englishKeys = GetKeys(CultureInfo.GetCultureInfo("en"));

        Assert.Equal(germanKeys, englishKeys);
    }

    [Fact]
    public void Get_UnknownKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => TrayTexts.Get("DoesNotExist"));
    }

    private static string InCulture(string culture, Func<string> text)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return text();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static SortedSet<string> GetKeys(CultureInfo culture)
    {
        var resourceSet = TrayTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No resources for culture '{culture.Name}'.");
        return new SortedSet<string>(resourceSet.Cast<DictionaryEntry>().Select(entry => (string)entry.Key));
    }
}
