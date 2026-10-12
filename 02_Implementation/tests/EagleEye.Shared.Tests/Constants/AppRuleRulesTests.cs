using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class AppRuleRulesTests
{
    [Fact]
    public void Defaults_AreZeroAndNoDay()
    {
        Assert.Equal((0, BreakTimeDays.None, 1439, 23, 59),
            (AppRuleRules.DefaultBudgetMinutes, AppRuleRules.DefaultDays, AppRuleRules.MaxBudgetMinutes, AppRuleRules.MaxHours, AppRuleRules.MaxMinutes));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1439, true)]
    [InlineData(-1, false)]
    [InlineData(1440, false)]
    public void IsValidBudget_Range(int minutes, bool expected)
    {
        Assert.Equal(expected, AppRuleRules.IsValidBudget(minutes));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(127, true)]
    [InlineData(128, false)]
    [InlineData(-1, false)]
    public void IsValidDays_AnyCombinationAlsoNone(int days, bool expected)
    {
        Assert.Equal(expected, AppRuleRules.IsValidDays((BreakTimeDays)days));
    }

    [Theory]
    [InlineData(AppBudgetPart.Hours, 0, true)]
    [InlineData(AppBudgetPart.Hours, 23, true)]
    [InlineData(AppBudgetPart.Hours, 24, false)]
    [InlineData(AppBudgetPart.Hours, -1, false)]
    [InlineData(AppBudgetPart.Minutes, 0, true)]
    [InlineData(AppBudgetPart.Minutes, 59, true)]
    [InlineData(AppBudgetPart.Minutes, 60, false)]
    [InlineData(AppBudgetPart.Minutes, -1, false)]
    [InlineData((AppBudgetPart)9, 1, false)]
    public void IsValidPart_Ranges(AppBudgetPart part, int value, bool expected)
    {
        Assert.Equal(expected, AppRuleRules.IsValidPart(part, value));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public void IsAllowedOn_EveryDay_TickedAndBudget(DayOfWeek day)
    {
        var only = BreakTimeRules.ToDays(day);

        Assert.True(AppRuleRules.IsAllowedOn(1, only, day));
        Assert.False(AppRuleRules.IsAllowedOn(1, BreakTimeDays.All & ~only, day));
        Assert.False(AppRuleRules.IsAllowedOn(0, BreakTimeDays.All, day));
    }

    [Theory]
    [InlineData(45, AppBudgetPart.Hours, 2, 165)]
    [InlineData(125, AppBudgetPart.Minutes, 7, 127)]
    [InlineData(0, AppBudgetPart.Hours, 23, 1380)]
    [InlineData(1380, AppBudgetPart.Minutes, 59, 1439)]
    [InlineData(165, AppBudgetPart.Hours, 0, 45)]
    public void Combine_KeepsTheOtherPart(int stored, AppBudgetPart part, int value, int expected)
    {
        Assert.Equal(expected, AppRuleRules.Combine(stored, part, value));
    }

    [Theory]
    [InlineData(-1, AppBudgetPart.Hours, 1)]
    [InlineData(1440, AppBudgetPart.Hours, 1)]
    [InlineData(0, AppBudgetPart.Hours, 24)]
    [InlineData(0, AppBudgetPart.Minutes, 60)]
    public void Combine_OutOfRange_Throws(int stored, AppBudgetPart part, int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AppRuleRules.Combine(stored, part, value));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(45, "0:45")]
    [InlineData(185, "3:05")]
    [InlineData(1439, "23:59")]
    public void Format_HMM(int minutes, string expected)
    {
        Assert.Equal(expected, AppRuleRules.Format(minutes));
        Assert.Equal(minutes / 60, AppRuleRules.HoursOf(minutes));
        Assert.Equal(minutes % 60, AppRuleRules.MinutesOf(minutes));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1440)]
    public void Format_OutOfRange_Throws(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AppRuleRules.Format(minutes));
    }
}
