using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.ParentApp.Tests.Fakes;
using System.Globalization;
using Xunit;

namespace EagleEye.ParentApp.Tests.Rules;

public sealed class AppBudgetTextTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("07", 7)]
    [InlineData(" 23 ", 23)]
    public void TryParseHours_Accepted(string text, int expected)
    {
        Assert.True(AppBudgetText.TryParseHours(text, out var hours));
        Assert.Equal(expected, hours);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("24")]
    [InlineData("007")]
    [InlineData("٣")]
    public void TryParseHours_Rejected(string? text)
    {
        Assert.False(AppBudgetText.TryParseHours(text, out _));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("59", true)]
    [InlineData("60", false)]
    [InlineData("1.5", false)]
    public void TryParseMinutes(string text, bool expected)
    {
        Assert.Equal(expected, AppBudgetText.TryParseMinutes(text, out _));
    }

    [Fact]
    public void Format_NoLeadingZero()
    {
        Assert.Equal(("0", "45"), (AppBudgetText.Format(0), AppBudgetText.Format(45)));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("59", true)]
    [InlineData("0", false)]
    [InlineData("60", false)]
    [InlineData("abc", false)]
    [InlineData("2.5", false)]
    [InlineData("", false)]
    public void WarningEditRules_TryParseMinutes(string text, bool expected)
    {
        Assert.Equal(expected, WarningEditRules.TryParseMinutes(text, out _));
    }

    [Theory]
    [InlineData(3, 3, false)]
    [InlineData(3, 4, false)]
    [InlineData(5, 1, true)]
    public void WarningEditRules_IsValidPair(int early, int final, bool expected)
    {
        Assert.Equal(expected, WarningEditRules.IsValidPair(early, final));
    }
}

public sealed class KidTextEditingTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private const string EnglishBudget = "You have used up your time! Sorry, that's it for now.";
    private const string GermanBudget = "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.";

    [Fact]
    public void ShownText_ParentTextOrTheDefaultInTheCulture()
    {
        Assert.Equal("Ende", KidTextEditing.ShownText("Ende", DefaultTextKind.BudgetText, English));
        Assert.Equal(EnglishBudget, KidTextEditing.ShownText(null, DefaultTextKind.BudgetText, English));
    }

    [Theory]
    [InlineData("Ende", "Ende", false, null)]
    [InlineData("Ende\r\n", "Ende\n", false, null)]
    [InlineData("Neu", "Ende", false, "Neu")]
    [InlineData("Neu\r\nZeile", EnglishBudget, true, "Neu\nZeile")]
    [InlineData("", "Ende", false, "")]
    [InlineData("  ", "Ende", false, "")]
    [InlineData(GermanBudget, "Ende", false, "")]
    [InlineData(GermanBudget, EnglishBudget, true, null)]
    [InlineData("", EnglishBudget, true, null)]
    public void Commit_Rules(string edited, string confirmed, bool isDefault, string? expected)
    {
        Assert.Equal(expected, KidTextEditing.Commit(edited, confirmed, isDefault, DefaultTextKind.BudgetText));
    }

    [Fact]
    public void Commit_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => KidTextEditing.Commit(null!, "a", false, DefaultTextKind.BudgetText));
        Assert.Throws<ArgumentNullException>(() => KidTextEditing.Commit("a", null!, false, DefaultTextKind.BudgetText));
    }

    [Fact]
    public void ShownText_GermanUi_GermanDefault()
    {
        Assert.Equal(GermanBudget, TestSupport.InCulture("de-DE", () => KidTextEditing.ShownText(null, DefaultTextKind.BudgetText, CultureInfo.CurrentUICulture)));
    }
}
