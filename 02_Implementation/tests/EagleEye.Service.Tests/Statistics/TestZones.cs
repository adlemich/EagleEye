namespace EagleEye.Service.Tests.Statistics;

/// <summary>Time zones for the day-split tests.</summary>
internal static class TestZones
{
    /// <summary>Central Europe: UTC+1, DST UTC+2 from the last Sunday of March 02:00 to the last Sunday of October 03:00.</summary>
    public static readonly TimeZoneInfo Berlin = TimeZoneInfo.CreateCustomTimeZone(
        "Test Berlin", TimeSpan.FromHours(1), "Test Berlin", "Test Berlin", "Test Berlin Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
        ]);

    /// <summary>A zone whose DST starts at local midnight (midnight does not exist that day), first Sunday of November.</summary>
    public static readonly TimeZoneInfo MidnightDst = TimeZoneInfo.CreateCustomTimeZone(
        "Test Midnight", TimeSpan.FromHours(-3), "Test Midnight", "Test Midnight", "Test Midnight Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 11, 1, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 2, 3, DayOfWeek.Sunday)),
        ]);
}
