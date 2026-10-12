using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>The next begin of a break entry (US-006 Decision 4).</summary>
/// <param name="Entry">The entry.</param>
/// <param name="StartLocal">The local wall-clock start of this occurrence.</param>
/// <param name="Until">Real time from now until the start.</param>
public sealed record BreakBegin(BreakTimeEntryDto Entry, DateTime StartLocal, TimeSpan Until);

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

    /// <summary>
    /// The next break begin within the horizon (US-006 Decision 4): the earliest start <c>t</c> of an active entry whose
    /// weekday (of <c>t</c>) is ticked, with now &lt; t ≤ now + horizon, looking at today and tomorrow (an entry starting at
    /// 00:00 counts for the new day). Wall-clock starts: a start inside a skipped DST hour does not exist; in a repeated
    /// hour the first pass counts. Ties go to the first entry (creation order). An entry in effect now is the caller's case.
    /// </summary>
    /// <returns>The begin, or <c>null</c> if no entry begins within the horizon.</returns>
    public static BreakBegin? NextBegin(IEnumerable<BreakTimeEntryDto> entries, DateTimeOffset nowUtc, TimeZoneInfo zone, TimeSpan horizon)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(zone);
        var today = ToLocal(nowUtc, zone).Date;
        BreakBegin? best = null;
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            foreach (var day in (ReadOnlySpan<DateTime>)[today, today.AddDays(1)])
            {
                var start = day.AddMinutes(entry.StartMinute);
                if ((entry.Days & BreakTimeRules.ToDays(start.DayOfWeek)) == BreakTimeDays.None || zone.IsInvalidTime(start))
                {
                    continue;
                }

                var until = FirstPassUtc(start, zone) - nowUtc;
                if (until > TimeSpan.Zero && until <= horizon && (best is null || until < best.Until))
                {
                    best = new BreakBegin(entry, start, until);
                }
            }
        }

        return best;
    }

    /// <summary>The UTC moment of a valid local wall-clock time; in a repeated hour the first pass.</summary>
    public static DateTimeOffset FirstPassUtc(DateTime local, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = zone.IsAmbiguousTime(unspecified) ? zone.GetAmbiguousTimeOffsets(unspecified).Max() : zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    /// <summary>The local wall-clock time of a UTC moment in the zone.</summary>
    public static DateTime ToLocal(DateTimeOffset utc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTime(utc, zone).DateTime;
    }
}
