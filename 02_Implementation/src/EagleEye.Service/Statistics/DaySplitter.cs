namespace EagleEye.Service.Statistics;

/// <summary>
/// Places a credited interval on the local time line and cuts it at local midnights (US-004 AC-14, ADR-012 §4).
/// The interval is <c>[end − length, end]</c> in absolute time; each part counts for its local date.
/// </summary>
public static class DaySplitter
{
    /// <summary>Returns the parts of the interval per local date, in time order.</summary>
    public static IReadOnlyList<(DateOnly Day, TimeSpan Length)> Split(DateTimeOffset end, TimeSpan length, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, TimeSpan.Zero);

        var parts = new List<(DateOnly, TimeSpan)>();
        var start = end - length;
        while (start < end)
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
            var boundary = StartOfDay(day.AddDays(1), zone);
            var partEnd = boundary < end ? boundary : end;
            parts.Add((day, partEnd - start));
            start = partEnd;
        }

        return parts;
    }

    /// <summary>The first instant of a local date (the first valid local time if midnight falls into a DST gap).</summary>
    internal static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(15);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
