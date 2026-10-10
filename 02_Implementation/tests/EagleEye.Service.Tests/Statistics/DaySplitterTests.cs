using EagleEye.Service.Statistics;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class DaySplitterTests
{
    private static readonly DateOnly Oct7 = new(2026, 10, 7);
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    [Fact]
    public void Split_InsideOneDay_OnePart()
    {
        var end = Local(2026, 10, 7, 15, 0, 0);

        Assert.Equal([(Oct7, TimeSpan.FromSeconds(5))], DaySplitter.Split(end, TimeSpan.FromSeconds(5), TestZones.Berlin));
    }

    [Fact]
    public void Split_AcrossMidnight_PartsPerDay()
    {
        var end = Local(2026, 10, 8, 0, 0, 2);

        Assert.Equal(
            [(Oct7, TimeSpan.FromSeconds(3)), (Oct8, TimeSpan.FromSeconds(2))],
            DaySplitter.Split(end, TimeSpan.FromSeconds(5), TestZones.Berlin));
    }

    [Fact]
    public void Split_EndingExactlyAtMidnight_OldDay()
    {
        var end = Local(2026, 10, 8, 0, 0, 0);

        Assert.Equal([(Oct7, TimeSpan.FromSeconds(5))], DaySplitter.Split(end, TimeSpan.FromSeconds(5), TestZones.Berlin));
    }

    [Fact]
    public void Split_StartingExactlyAtMidnight_NewDay()
    {
        var end = Local(2026, 10, 8, 0, 0, 5);

        Assert.Equal([(Oct8, TimeSpan.FromSeconds(5))], DaySplitter.Split(end, TimeSpan.FromSeconds(5), TestZones.Berlin));
    }

    [Fact]
    public void Split_ZeroLength_NoParts()
    {
        Assert.Empty(DaySplitter.Split(Local(2026, 10, 7, 15, 0, 0), TimeSpan.Zero, TestZones.Berlin));
    }

    [Fact]
    public void Split_WholeDstSpringDay_Is23Hours()
    {
        // 2026-03-29: clocks go from 02:00 to 03:00.
        var end = Local(2026, 3, 30, 0, 0, 0);

        Assert.Equal([(new DateOnly(2026, 3, 29), TimeSpan.FromHours(23))], DaySplitter.Split(end, TimeSpan.FromHours(23), TestZones.Berlin));
    }

    [Fact]
    public void Split_WholeDstAutumnDay_Is25Hours()
    {
        // 2026-10-25: clocks go from 03:00 back to 02:00.
        var end = Local(2026, 10, 26, 0, 0, 0);

        var parts = DaySplitter.Split(end, TimeSpan.FromHours(26), TestZones.Berlin);

        Assert.Equal([(new DateOnly(2026, 10, 24), TimeSpan.FromHours(1)), (new DateOnly(2026, 10, 25), TimeSpan.FromHours(25))], parts);
    }

    [Fact]
    public void Split_DstStartingAtMidnight_DayStartsAtFirstValidTime()
    {
        // 2026-11-01 (first Sunday of November): 00:00 does not exist, the day starts at 01:00 local (UTC-2).
        var end = new DateTimeOffset(2026, 11, 1, 3, 0, 5, TimeSpan.Zero); // 01:00:05 local

        Assert.Equal(
            [(new DateOnly(2026, 10, 31), TimeSpan.FromSeconds(5)), (new DateOnly(2026, 11, 1), TimeSpan.FromSeconds(5))],
            DaySplitter.Split(end, TimeSpan.FromSeconds(10), TestZones.MidnightDst));
    }

    [Fact]
    public void Split_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => DaySplitter.Split(DateTimeOffset.UnixEpoch, TimeSpan.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => DaySplitter.Split(DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(-1), TestZones.Berlin));
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int second)
    {
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TestZones.Berlin.GetUtcOffset(local));
    }
}
