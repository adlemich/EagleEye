using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// Pure evaluation of break-time entries (TC-014, AC-21, ADR-013 §2): an entry is in effect at a local date-time when
/// it is active, the local weekday is ticked and start ≤ time &lt; end, where an end of 23:59 means 24:00. Wall-clock
/// semantics: the caller converts UTC to the local zone, so skipped DST hours never occur and repeated ones occur twice.
/// </summary>
public static class BreakTimeSchedule
{
    private static readonly TimeSpan Midnight = TimeSpan.FromDays(1);

    /// <summary>Whether the entry is in effect at the local date-time.</summary>
    public static bool InEffect(BreakTimeEntryDto entry, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsActive || (entry.Days & BreakTimeRules.ToDays(local.DayOfWeek)) == BreakTimeDays.None)
        {
            return false;
        }

        var time = local.TimeOfDay;
        var end = entry.EndMinute == BreakTimeRules.LastMinute ? Midnight : TimeSpan.FromMinutes(entry.EndMinute);
        return time >= TimeSpan.FromMinutes(entry.StartMinute) && time < end;
    }

    /// <summary>The first entry (creation order) in effect at the local date-time, or <c>null</c>.</summary>
    public static BreakTimeEntryDto? FirstInEffect(IEnumerable<BreakTimeEntryDto> entries, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.FirstOrDefault(e => InEffect(e, local));
    }

    /// <summary>The local wall-clock time of a UTC moment in the zone.</summary>
    public static DateTime ToLocal(DateTimeOffset utc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTime(utc, zone).DateTime;
    }
}
