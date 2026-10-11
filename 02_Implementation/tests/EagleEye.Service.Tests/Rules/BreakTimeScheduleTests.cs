using EagleEye.Service.Rules;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class BreakTimeScheduleTests
{
    // 2026-10-12 is a Monday.
    private static readonly BreakTimeEntryDto MondayEight = new(1, true, 1200, 1260, BreakTimeDays.Monday);
    private static readonly BreakTimeEntryDto MondayUntilMidnight = new(2, true, 1200, 1439, BreakTimeDays.Monday);

    [Theory]
    [InlineData(12, 19, 59, 59, false)]
    [InlineData(12, 20, 0, 0, true)]
    [InlineData(12, 20, 59, 59, true)]
    [InlineData(12, 21, 0, 0, false)]
    [InlineData(13, 20, 30, 0, false)]
    public void InEffect_Ac21Examples_TwentyToTwentyOneMonday(int day, int hour, int minute, int second, bool expected)
    {
        Assert.Equal(expected, BreakTimeSchedule.InEffect(MondayEight, new DateTime(2026, 10, day, hour, minute, second)));
    }

    [Theory]
    [InlineData(12, 23, 59, 30, true)]
    [InlineData(12, 23, 59, 59, true)]
    [InlineData(13, 0, 0, 0, false)]
    public void InEffect_EndOf2359MeansMidnight(int day, int hour, int minute, int second, bool expected)
    {
        Assert.Equal(expected, BreakTimeSchedule.InEffect(MondayUntilMidnight, new DateTime(2026, 10, day, hour, minute, second)));
    }

    [Fact]
    public void InEffect_EndOf2358_EndsAt2358()
    {
        var entry = MondayUntilMidnight with { EndMinute = 1438 };

        Assert.False(BreakTimeSchedule.InEffect(entry, new DateTime(2026, 10, 12, 23, 58, 0)));
    }

    [Fact]
    public void InEffect_FromMidnight_StartIsInclusive()
    {
        var entry = new BreakTimeEntryDto(1, true, 0, 540, BreakTimeDays.Tuesday);

        Assert.True(BreakTimeSchedule.InEffect(entry, new DateTime(2026, 10, 13, 0, 0, 0)));
    }

    [Fact]
    public void InEffect_Inactive_Never()
    {
        Assert.False(BreakTimeSchedule.InEffect(MondayEight with { IsActive = false }, new DateTime(2026, 10, 12, 20, 30, 0)));
    }

    [Theory]
    [InlineData(12, BreakTimeDays.Monday)]
    [InlineData(13, BreakTimeDays.Tuesday)]
    [InlineData(14, BreakTimeDays.Wednesday)]
    [InlineData(15, BreakTimeDays.Thursday)]
    [InlineData(16, BreakTimeDays.Friday)]
    [InlineData(17, BreakTimeDays.Saturday)]
    [InlineData(18, BreakTimeDays.Sunday)]
    public void InEffect_OnlyOnTickedDays(int day, BreakTimeDays ticked)
    {
        var entry = MondayEight with { Days = ticked };
        var local = new DateTime(2026, 10, day, 20, 30, 0);

        Assert.True(BreakTimeSchedule.InEffect(entry, local));
        Assert.False(BreakTimeSchedule.InEffect(entry with { Days = BreakTimeDays.All & ~ticked }, local));
    }

    [Fact]
    public void FirstInEffect_Overlap_FirstByOrder()
    {
        var early = new BreakTimeEntryDto(5, true, 1080, 1260, BreakTimeDays.All);

        Assert.Same(early, BreakTimeSchedule.FirstInEffect([early, MondayUntilMidnight], new DateTime(2026, 10, 12, 20, 30, 0)));
        Assert.Same(MondayUntilMidnight, BreakTimeSchedule.FirstInEffect([early, MondayUntilMidnight], new DateTime(2026, 10, 12, 21, 30, 0)));
    }

    [Fact]
    public void FirstInEffect_None_Null()
    {
        Assert.Null(BreakTimeSchedule.FirstInEffect([MondayEight with { IsActive = false }, MondayUntilMidnight], new DateTime(2026, 10, 12, 19, 0, 0)));
    }

    [Fact]
    public void DstSpring_SkippedHourNeverInEffect()
    {
        // 2026-03-29 (Sunday): 02:00 local jumps to 03:00.
        var entry = new BreakTimeEntryDto(1, true, 120, 180, BreakTimeDays.Sunday);
        var justBefore = new DateTimeOffset(2026, 3, 29, 0, 59, 59, TimeSpan.Zero);

        Assert.False(BreakTimeSchedule.InEffect(entry, BreakTimeSchedule.ToLocal(justBefore, TestZones.Berlin)));
        Assert.False(BreakTimeSchedule.InEffect(entry, BreakTimeSchedule.ToLocal(justBefore.AddSeconds(1), TestZones.Berlin)));
    }

    [Fact]
    public void DstAutumn_RepeatedHourInEffectInBothPasses()
    {
        // 2026-10-25 (Sunday): 03:00 local goes back to 02:00, so 02:30 happens at 00:30 and 01:30 UTC.
        var entry = new BreakTimeEntryDto(1, true, 120, 180, BreakTimeDays.Sunday);

        Assert.True(BreakTimeSchedule.InEffect(entry, BreakTimeSchedule.ToLocal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), TestZones.Berlin)));
        Assert.True(BreakTimeSchedule.InEffect(entry, BreakTimeSchedule.ToLocal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), TestZones.Berlin)));
    }

    [Fact]
    public void ToLocal_ZoneSwitch_UsesTheNewZone()
    {
        var utc = new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero);
        var pacific = TimeZoneInfo.CreateCustomTimeZone("Test Pacific", TimeSpan.FromHours(-8), "Test Pacific", "Test Pacific");

        Assert.True(BreakTimeSchedule.InEffect(MondayEight, BreakTimeSchedule.ToLocal(utc, TestZones.Berlin)));
        Assert.False(BreakTimeSchedule.InEffect(MondayEight, BreakTimeSchedule.ToLocal(utc, pacific)));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.InEffect(null!, DateTime.Now));
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.FirstInEffect(null!, DateTime.Now));
        Assert.Throws<ArgumentNullException>(() => BreakTimeSchedule.ToLocal(DateTimeOffset.UtcNow, null!));
    }
}
