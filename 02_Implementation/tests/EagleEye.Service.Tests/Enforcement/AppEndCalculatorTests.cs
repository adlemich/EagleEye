using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

/// <summary>2026-10-12 is a Monday; Berlin is UTC+2 that day.</summary>
public sealed class AppEndCalculatorTests
{
    private static readonly BreakTimeEntryDto Evening = new(3, true, 1200, 1439, BreakTimeDays.All);

    [Fact]
    public void BudgetAlone_SecondsOfUseLeft()
    {
        var end = Compute(Rule(60), 1800, [], 10, 0);

        Assert.Equal(new AppEnd(AppEndKind.Budget, 1800, null, null), end);
    }

    [Fact]
    public void BudgetUsedUp_Zero()
    {
        Assert.Equal(new AppEnd(AppEndKind.Budget, 0, null, null), Compute(Rule(3), 200, [], 10, 0));
    }

    [Fact]
    public void BudgetOutsideTheHorizon_NoEnd()
    {
        Assert.Null(Compute(Rule(120), 0, [], 10, 0));
    }

    [Theory]
    [InlineData(30, BreakTimeDays.Saturday)]
    [InlineData(0, BreakTimeDays.All)]
    public void NotAllowedToday_Zero(int budget, BreakTimeDays days)
    {
        Assert.Equal(new AppEnd(AppEndKind.NotAllowedToday, 0, null, null), Compute(Rule(budget, days), 0, [], 10, 0));
    }

    [Fact]
    public void DefaultRule_NotAllowedToday()
    {
        Assert.Equal(AppEndKind.NotAllowedToday, Compute(null, 0, [], 10, 0)!.Kind);
    }

    [Fact]
    public void BreakInEffect_ZeroWithTheOccurrenceStart_WinsTheTie()
    {
        var end = Compute(Rule(0), 0, [Evening], 20, 10);

        Assert.Equal(new AppEnd(AppEndKind.Break, 0, Evening, new DateTime(2026, 10, 12, 20, 0, 0)), end);
    }

    [Fact]
    public void BreakBeforeTheBudget_Break()
    {
        var end = Compute(Rule(60), 2400, [Evening], 19, 50);

        Assert.Equal((AppEndKind.Break, 600.0), (end!.Kind, end.SecondsLeft));
    }

    [Fact]
    public void BudgetBeforeTheBreak_Budget()
    {
        var end = Compute(Rule(60), 3300, [Evening], 19, 50);

        Assert.Equal((AppEndKind.Budget, 300.0), (end!.Kind, end.SecondsLeft));
    }

    [Fact]
    public void BudgetAndBreakAtTheSameTime_Break()
    {
        Assert.Equal(AppEndKind.Break, Compute(Rule(60), 3000, [Evening], 19, 50)!.Kind);
    }

    [Fact]
    public void NextDayNotAllowed_Midnight()
    {
        var end = Compute(Rule(1439, BreakTimeDays.Monday), 0, [], 23, 30);

        Assert.Equal(new AppEnd(AppEndKind.Midnight, 1800, null, null), end);
    }

    [Fact]
    public void BudgetEndAfterMidnight_NextDayAllowed_NoEnd()
    {
        Assert.Null(Compute(Rule(60), 0, [], 23, 30));
    }

    [Fact]
    public void BudgetBeforeMidnight_NextDayNotAllowed_Budget()
    {
        var end = Compute(Rule(10, BreakTimeDays.Monday), 0, [], 23, 30);

        Assert.Equal((AppEndKind.Budget, 600.0), (end!.Kind, end.SecondsLeft));
    }

    [Fact]
    public void MidnightInsideASkippedHour_MovesToItsEnd()
    {
        // TestZones.MidnightDst: 2026-11-01 (Sunday) 00:00 → 01:00. Saturday 23:30 local = 02:30 UTC.
        var now = new DateTimeOffset(2026, 11, 1, 2, 30, 0, TimeSpan.Zero);

        var end = AppEndCalculator.Compute(Rule(1439, BreakTimeDays.Saturday), 0, [], now, TestZones.MidnightDst);

        Assert.Equal((AppEndKind.Midnight, 1800.0), (end!.Kind, end.SecondsLeft));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AppEndCalculator.Compute(null, 0, null!, DateTimeOffset.UtcNow, TestZones.Berlin));
        Assert.Throws<ArgumentNullException>(() => AppEndCalculator.Compute(null, 0, [], DateTimeOffset.UtcNow, null!));
    }

    private static AppRuleEntry Rule(int budget, BreakTimeDays days = BreakTimeDays.All) => new(1, "p", "App", budget, days);

    private static AppEnd? Compute(AppRuleEntry? rule, long used, IReadOnlyList<BreakTimeEntryDto> entries, int hour, int minute) =>
        AppEndCalculator.Compute(rule, used, entries, new DateTimeOffset(2026, 10, 12, hour, minute, 0, TimeSpan.FromHours(2)), TestZones.Berlin);
}
