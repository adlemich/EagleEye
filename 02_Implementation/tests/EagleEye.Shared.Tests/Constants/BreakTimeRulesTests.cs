using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class BreakTimeRulesTests
{
    [Fact]
    public void Defaults_MatchTheStory()
    {
        Assert.Equal(
            (20, 500, 1439, 1200, 1439, BreakTimeDays.All),
            (BreakTimeRules.MaxEntriesPerAccount, BreakTimeRules.MaxDisplayTextLength, BreakTimeRules.LastMinute,
             BreakTimeRules.DefaultStartMinute, BreakTimeRules.DefaultEndMinute, BreakTimeRules.DefaultDays));
    }

    [Fact]
    public void DefaultDisplayText_IsTheGermanTextOfAc15()
    {
        Assert.Equal(
            "Hi! Leider haben Deine Eltern eine PC-Pause für diese Uhrzeit eingestellt. Du kannst dieses Programm jetzt nicht verwenden. Tut mir leid. Wie wäre es wenn Du die Zeit nutzt, um ein Buch zu lesen? \U0001F60A",
            BreakTimeRules.DefaultDisplayText);
        Assert.True(BreakTimeRules.IsValidDisplayText(BreakTimeRules.DefaultDisplayText));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, BreakTimeDays.Monday)]
    [InlineData(DayOfWeek.Tuesday, BreakTimeDays.Tuesday)]
    [InlineData(DayOfWeek.Wednesday, BreakTimeDays.Wednesday)]
    [InlineData(DayOfWeek.Thursday, BreakTimeDays.Thursday)]
    [InlineData(DayOfWeek.Friday, BreakTimeDays.Friday)]
    [InlineData(DayOfWeek.Saturday, BreakTimeDays.Saturday)]
    [InlineData(DayOfWeek.Sunday, BreakTimeDays.Sunday)]
    public void ToDays_EveryWeekday_ReturnsItsFlag(DayOfWeek day, BreakTimeDays expected)
    {
        Assert.Equal(expected, BreakTimeRules.ToDays(day));
    }

    [Fact]
    public void ToDays_UnknownValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakTimeRules.ToDays((DayOfWeek)7));
    }

    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("a\r\n\r\nb\r", "a\n\nb\n")]
    [InlineData("", "")]
    public void NormalizeLineBreaks_ReturnsOnlyLineFeeds(string input, string expected)
    {
        Assert.Equal(expected, BreakTimeRules.NormalizeLineBreaks(input));
    }

    [Fact]
    public void NormalizeLineBreaks_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BreakTimeRules.NormalizeLineBreaks(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hallo\nWelt\tTab")]
    [InlineData("\U0001F60A \U0001F4DA \U0001F44D")]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467 \U0001F44D\U0001F3FD ❤️")]
    public void IsValidDisplayText_AllowedTexts_True(string text)
    {
        Assert.True(BreakTimeRules.IsValidDisplayText(text));
    }

    [Fact]
    public void IsValidDisplayText_ExactlyMaxLength_True()
    {
        Assert.True(BreakTimeRules.IsValidDisplayText(new string('a', BreakTimeRules.MaxDisplayTextLength)));
    }

    [Fact]
    public void IsValidDisplayText_OneOverMaxLength_False()
    {
        Assert.False(BreakTimeRules.IsValidDisplayText(new string('a', BreakTimeRules.MaxDisplayTextLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a\rb")]
    [InlineData("a\u0000b")]
    [InlineData("a\u001Bb")]
    [InlineData("a\u0085b")]
    public void IsValidDisplayText_RejectedTexts_False(string? text)
    {
        Assert.False(BreakTimeRules.IsValidDisplayText(text));
    }

    [Theory]
    [InlineData("a", '\uD83D', "")]
    [InlineData("", '\uD83D', "a")]
    [InlineData("a", '\uDE0A', "b")]
    public void IsValidDisplayText_LoneSurrogate_False(string before, char surrogate, string after)
    {
        // Built at run time: attribute metadata cannot carry lone surrogates.
        var text = before + surrogate + after;

        Assert.False(BreakTimeRules.IsValidDisplayText(text));
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1200, 1439, true)]
    [InlineData(1438, 1439, true)]
    [InlineData(1200, 1200, false)]
    [InlineData(1200, 1140, false)]
    [InlineData(-1, 10, false)]
    [InlineData(0, 1440, false)]
    [InlineData(1439, 1440, false)]
    public void IsValidTimes_Bounds(int start, int end, bool expected)
    {
        Assert.Equal(expected, BreakTimeRules.IsValidTimes(start, end));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1439, true)]
    [InlineData(1440, false)]
    public void IsValidMinute_Bounds(int minute, bool expected)
    {
        Assert.Equal(expected, BreakTimeRules.IsValidMinute(minute));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(540, "09:00")]
    [InlineData(1200, "20:00")]
    [InlineData(1439, "23:59")]
    [InlineData(450, "07:30")]
    public void FormatMinute_ReturnsHhMm(int minute, string expected)
    {
        Assert.Equal(expected, BreakTimeRules.FormatMinute(minute));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1440)]
    public void FormatMinute_OutOfRange_Throws(int minute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakTimeRules.FormatMinute(minute));
    }
}
