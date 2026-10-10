using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>Why the app refuses an edit before sending it (AC-9 to AC-11).</summary>
public enum EditProblem
{
    /// <summary>The edit is valid.</summary>
    None,

    /// <summary>Not a time from 00:00 to 23:59 (AC-9).</summary>
    InvalidTime,

    /// <summary>The end would not be later than the start (AC-10).</summary>
    EndNotAfterStart,

    /// <summary>The last ticked day would be unticked (AC-11).</summary>
    NoDaySelected,
}

/// <summary>
/// Client-side validation of break-time edits against the row's confirmed values (the service validates again
/// against its stored values, AC-19).
/// </summary>
public static class BreakTimeEditRules
{
    /// <summary>Whether a typed start or end time may be sent.</summary>
    public static EditProblem CheckTime(BreakTimeEntryDto confirmed, BreakTimeBoundary boundary, string? text, out int minute)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        if (!TimeOfDayText.TryParse(text, out minute))
        {
            return EditProblem.InvalidTime;
        }

        var (start, end) = boundary == BreakTimeBoundary.Start ? (minute, confirmed.EndMinute) : (confirmed.StartMinute, minute);
        return BreakTimeRules.IsValidTimes(start, end) ? EditProblem.None : EditProblem.EndNotAfterStart;
    }

    /// <summary>Whether a weekday may be ticked or unticked: at least one day stays ticked.</summary>
    public static EditProblem CheckDay(BreakTimeDays confirmedDays, DayOfWeek day, bool isSelected)
    {
        var days = isSelected ? confirmedDays | BreakTimeRules.ToDays(day) : confirmedDays & ~BreakTimeRules.ToDays(day);
        return days == BreakTimeDays.None ? EditProblem.NoDaySelected : EditProblem.None;
    }

    /// <summary>Whether another entry may be added (OQ-8).</summary>
    public static bool CanAdd(int entryCount) => entryCount < BreakTimeRules.MaxEntriesPerAccount;
}
