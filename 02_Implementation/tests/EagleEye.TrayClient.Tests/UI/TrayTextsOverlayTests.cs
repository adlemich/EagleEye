using System.Collections;
using System.Globalization;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

/// <summary>US-010 Localization Impact: the new menu and overlay keys, the removed warning info lines.</summary>
public sealed class TrayTextsOverlayTests
{
    public static TheoryData<string, string, string> NewKeys => new()
    {
        { "MenuOpacity", "Deckkraft", "Opacity" },
        { "MenuPosition", "Position", "Position" },
        { "MenuShowFrom", "Anzeigen ab", "Show from" },
        { "MenuWarningSound", "Warnton", "Warning sound" },
        { "OpacityFormat", "{0} %", "{0} %" },
        { "CornerTopLeft", "Oben links", "Top left" },
        { "CornerTopRight", "Oben rechts", "Top right" },
        { "CornerBottomLeft", "Unten links", "Bottom left" },
        { "CornerBottomRight", "Unten rechts", "Bottom right" },
        { "ShowFromFormat", "{0} Min.", "{0} min" },
        { "BreakLineFormat", "Ruhezeit in {0}", "Break in {0}" },
        { "MoreLinesFormat", "+{0} weitere", "+{0} more" },
    };

    [Theory]
    [MemberData(nameof(NewKeys))]
    public void NewKeys_GermanAndEnglish(string key, string german, string english)
    {
        Assert.Equal((german, english), (TrayTexts.Get(key, CultureInfo.GetCultureInfo("de-DE")), TrayTexts.Get(key, CultureInfo.GetCultureInfo("en-US"))));
    }

    [Fact]
    public void RemovedKeys_GoneInBothLanguages()
    {
        string[] removed = ["InfoMinutesLeftOne", "InfoMinutesLeftMany", "InfoBreakBegins", "InfoAppGraceOne", "InfoAppGraceMany", "InfoBreakGraceOne", "InfoBreakGraceMany"];
        var german = Keys(CultureInfo.InvariantCulture);
        var english = Keys(CultureInfo.GetCultureInfo("en"));

        Assert.All(removed, key => Assert.DoesNotContain(key, german));
        Assert.All(removed, key => Assert.DoesNotContain(key, english));
    }

    private static HashSet<string> Keys(CultureInfo culture) =>
        [.. TrayTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!.Cast<DictionaryEntry>().Select(e => (string)e.Key)];
}
