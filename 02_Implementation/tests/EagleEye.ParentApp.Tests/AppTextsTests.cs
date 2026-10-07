using System.Collections;
using System.Globalization;
using System.Reflection;
using EagleEye.ParentApp.Core;
using EagleEye.ParentApp.Tests.Fakes;
using Xunit;

namespace EagleEye.ParentApp.Tests;

public sealed class AppTextsTests
{
    private static readonly PropertyInfo[] TextProperties = typeof(AppTexts)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.PropertyType == typeof(string))
        .ToArray();

    public static TheoryData<string> TextNames => new(TextProperties.Select(p => p.Name));

    [Theory]
    [MemberData(nameof(TextNames))]
    public void Text_German_IsDefined(string name)
    {
        var text = TestSupport.InCulture("de-DE", () => Read(name));

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Theory]
    [MemberData(nameof(TextNames))]
    public void Text_EnglishUiCulture_ComesFromEnglishResource(string name)
    {
        var english = TestSupport.InCulture("en-US", () => Read(name));

        Assert.Equal(GetResource(CultureInfo.GetCultureInfo("en"), name), english);
    }

    [Theory]
    [InlineData("de-DE", "Einstellungen", "Kopplung aufheben")]
    [InlineData("en-US", "Settings", "Remove pairing")]
    [InlineData("en-GB", "Settings", "Remove pairing")]
    [InlineData("fr-FR", "Einstellungen", "Kopplung aufheben")]
    [InlineData("", "Einstellungen", "Kopplung aufheben")]
    public void Texts_SelectedByUiCulture_FallBackToGerman(string culture, string menu, string remove)
    {
        var texts = TestSupport.InCulture(culture, () => (AppTexts.MenuSettings, AppTexts.RemovePairingButton));

        Assert.Equal((menu, remove), texts);
    }

    [Fact]
    public void Resources_GermanAndEnglish_DefineTheSameKeys()
    {
        Assert.Equal(GetKeys(CultureInfo.InvariantCulture), GetKeys(CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void Resources_EveryKeyHasAnAccessor()
    {
        Assert.Equal(GetKeys(CultureInfo.InvariantCulture), new SortedSet<string>(TextProperties.Select(p => p.Name)));
    }

    [Fact]
    public void Get_UnknownKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => AppTexts.Get("DoesNotExist"));
    }

    [Fact]
    public void Format_UsesCurrentCulture()
    {
        var text = TestSupport.InCulture("de-DE", () => AppTexts.Format(AppTexts.StatusConnectedFormat, "kid-pc"));

        Assert.Equal("Verbunden mit kid-pc", text);
    }

    private static string Read(string name) => (string)typeof(AppTexts).GetProperty(name)!.GetValue(null)!;

    private static string? GetResource(CultureInfo culture, string name)
    {
        return AppTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(name);
    }

    private static SortedSet<string> GetKeys(CultureInfo culture)
    {
        var resourceSet = AppTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"No resources for culture '{culture.Name}'.");
        return new SortedSet<string>(resourceSet.Cast<DictionaryEntry>().Select(entry => (string)entry.Key));
    }
}
