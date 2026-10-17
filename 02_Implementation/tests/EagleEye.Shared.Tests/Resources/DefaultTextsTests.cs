using System.Globalization;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.Shared.Resources;
using Xunit;

namespace EagleEye.Shared.Tests.Resources;

public sealed class DefaultTextsTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static TheoryData<DefaultTextKind, string, string> Texts => new()
    {
        {
            DefaultTextKind.BudgetText,
            "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.",
            "You have used up your time! Sorry, that's it for now."
        },
        {
            DefaultTextKind.EarlyWarningText,
            "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt.",
            "Attention, you only have 5 minutes left! Save your progress."
        },
        {
            DefaultTextKind.FinalWarningText,
            "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!",
            "Attention!!! Only 1 minute left – save quickly now!"
        },
        {
            DefaultTextKind.BreakDisplayText,
            BreakTimeRules.DefaultDisplayText,
            "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. " +
            "How about using the time to read a book? \U0001F60A"
        },
    };

    [Theory]
    [MemberData(nameof(Texts))]
    public void Get_GermanAndEnglish(DefaultTextKind kind, string german, string english)
    {
        Assert.Equal(german, DefaultTexts.Get(kind, German));
        Assert.Equal(english, DefaultTexts.Get(kind, English));
    }

    [Fact]
    public void Get_OtherLanguage_FallsBackToGerman()
    {
        Assert.Equal(DefaultTexts.Get(DefaultTextKind.BudgetText, German), DefaultTexts.Get(DefaultTextKind.BudgetText, CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void Get_UnknownKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DefaultTexts.Get((DefaultTextKind)99, German));
    }

    [Fact]
    public void Get_MissingResource_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DefaultTexts.Get("DoesNotExist", German));
    }

    [Fact]
    public void Get_NullCulture_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DefaultTexts.Get(DefaultTextKind.BudgetText, null!));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void IsAnyDefault_GermanOrEnglish_True(DefaultTextKind kind, string german, string english)
    {
        Assert.True(DefaultTexts.IsAnyDefault(kind, german));
        Assert.True(DefaultTexts.IsAnyDefault(kind, english));
    }

    [Fact]
    public void IsAnyDefault_Variants_False()
    {
        var english = DefaultTexts.Get(DefaultTextKind.BudgetText, English);

        Assert.False(DefaultTexts.IsAnyDefault(DefaultTextKind.BudgetText, english + " "));
        Assert.False(DefaultTexts.IsAnyDefault(DefaultTextKind.BudgetText, english + "\r\n"));
        Assert.False(DefaultTexts.IsAnyDefault(DefaultTextKind.EarlyWarningText, english));
        Assert.False(DefaultTexts.IsAnyDefault(
            DefaultTextKind.BreakDisplayText, BreakTimeRules.DefaultDisplayText.Replace("\U0001F60A", "\U0001F600", StringComparison.Ordinal)));
    }

    [Fact]
    public void IsAnyDefault_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DefaultTexts.IsAnyDefault(DefaultTextKind.BudgetText, null!));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  \n ", true)]
    [InlineData("You have used up your time! Sorry, that's it for now.", true)]
    [InlineData("Schluss", false)]
    public void MeansDefault_EmptyWhiteSpaceOrDefault(string? text, bool expected)
    {
        Assert.Equal(expected, DefaultTexts.MeansDefault(DefaultTextKind.BudgetText, text));
    }

    [Fact]
    public void SupportedCultures_GermanAndEnglish()
    {
        Assert.Equal(["de", "en"], DefaultTexts.SupportedCultures.Select(c => c.Name));
    }

    [Fact]
    public void EveryKind_HasBothLanguagesInTheResources()
    {
        foreach (var kind in Enum.GetValues<DefaultTextKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(DefaultTexts.ResourceManager.GetString(kind.ToString(), CultureInfo.InvariantCulture)));
            Assert.NotEqual(DefaultTexts.Get(kind, German), DefaultTexts.Get(kind, English));
        }
    }
}
