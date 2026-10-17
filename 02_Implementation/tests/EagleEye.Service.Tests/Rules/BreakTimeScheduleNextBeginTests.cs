using EagleEye.Service.Rules;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class BreakTimeScheduleNextBeginTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromMinutes(60);
    private static readonly BreakTimeEntryDto Evening = new(3, true, 1200, 1439, BreakTimeDays.All);

    [Fact]
    public void LaterToday_WithinTheHorizon()
    {
        var begin = BreakTimeSchedule.NextBegin([Evening], Berlin(2026, 10, 12, 19, 55), TestZones.Berlin, Hour);

        Assert.Equal(new BreakBegin(Evening, new DateTime(2026, 10, 12, 20, 0, 0), TimeSpan.FromMinutes(5)), begin);
    }

    [Fact]
    public void TomorrowAtMidnight_CountsForTheNewDay()
    {
        var night = new BreakTimeEntryDto(4, true, 0, 420, BreakTimeDays.Tuesday);

        var begin = BreakTimeSchedule.NextBegin([night], Berlin(2026, 10, 12, 23, 30), TestZones.Berlin, Hour);

        Assert.Equal((new DateTime(2026, 10, 13, 0, 0, 0), TimeSpan.FromMinutes(30)), (begin!.StartLocal, begin.Until));
    }

    [Fact]
    public void TomorrowNotTicked_None()
    {
        var night = new BreakTimeEntryDto(4, true, 0, 420, BreakTimeDays.Wednesday);

        Assert.Null(BreakTimeSchedule.NextBegin([night], Berlin(2026, 10, 12, 23, 30), TestZones.Berlin, Hour));
    }

    [Fact]
    public void InactiveOrOutsideTheHorizon_None()
    {
        Assert.Null(BreakTimeSchedule.NextBegin([Evening with { IsActive = false }], Berlin(2026, 10, 12, 19, 55), TestZones.Berlin, Hour));
        Assert.Null(BreakTimeSchedule.NextBegin([Evening], Berlin(2026, 10, 12, 18, 59), TestZones.Berlin, Hour));
    }

    [Fact]
    public void ExactlyAtTheHorizon_Counts_AtTheStartItself_NotNext()
    {
        Assert.NotNull(BreakTimeSchedule.NextBegin([Evening], Berlin(2026, 10, 12, 19, 0), TestZones.Berlin, Hour));
        Assert.Null(BreakTimeSchedule.NextBegin([Evening], Berlin(2026, 10, 12, 20, 0), TestZones.Berlin, Hour));
    }

    [Fact]
    public void Tie_FirstEntryWins_EarlierWins()
    {
        var same = Evening with { EntryId = 9 };
        var earlier = new BreakTimeEntryDto(10, true, 1190, 1439, BreakTimeDays.All);

        Assert.Equal(3, BreakTimeSchedule.NextBegin([Evening, same], Berlin(2026, 10, 12, 19, 55), TestZones.Berlin, Hour)!.Entry.EntryId);
        Assert.Equal(10, BreakTimeSchedule.NextBegin([Evening, earlier], Berlin(2026, 10, 12, 19, 45), TestZones.Berlin, Hour)!.Entry.EntryId);
    }

    [Fact]
    public void DstSpring_StartInTheSkippedHour_DoesNotExist()
    {
        // 2026-03-29: 02:00 → 03:00 in Berlin.
        var entry = new BreakTimeEntryDto(5, true, 150, 240, BreakTimeDays.All);
        var now = new DateTimeOffset(2026, 3, 29, 0, 50, 0, TimeSpan.Zero); // 01:50 local

        Assert.Null(BreakTimeSchedule.NextBegin([entry], now, TestZones.Berlin, Hour));
    }

    [Fact]
    public void DstAutumn_RepeatedHour_FirstPass()
    {
        // 2026-10-25: 03:00 → 02:00 in Berlin; 02:30 exists twice, the first pass is 00:30 UTC.
        var entry = new BreakTimeEntryDto(5, true, 150, 240, BreakTimeDays.All);
        var now = new DateTimeOffset(2026, 10, 24, 23, 50, 0, TimeSpan.Zero); // 01:50 local (summer time)

        Assert.Equal(TimeSpan.FromMinutes(40), BreakTimeSchedule.NextBegin([entry], now, TestZones.Berlin, Hour)!.Until);
    }

    [Fact]
    public void FirstPassUtc_NormalAndRepeated()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.Zero), BreakTimeSchedule.FirstPassUtc(new DateTime(2026, 10, 12, 20, 0, 0), TestZones.Berlin));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), BreakTimeSchedule.FirstPassUtc(new DateTime(2026, 10, 25, 2, 30, 0), TestZones.Berlin));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.NextBegin(null!, DateTimeOffset.UtcNow, TestZones.Berlin, Hour));
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.NextBegin([], DateTimeOffset.UtcNow, null!, Hour));
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.FirstPassUtc(DateTime.Now, null!));
    }

    private static DateTimeOffset Berlin(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(2)).ToUniversalTime();
}
