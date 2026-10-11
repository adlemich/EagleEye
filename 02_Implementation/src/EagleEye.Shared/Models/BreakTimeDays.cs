namespace EagleEye.Shared.Models;

/// <summary>Weekdays of a break-time entry (bit mask; Monday first as in the UI, TC-010).</summary>
[Flags]
public enum BreakTimeDays
{
    /// <summary>No day (never valid for a stored entry).</summary>
    None = 0,

    /// <summary>Monday.</summary>
    Monday = 1,

    /// <summary>Tuesday.</summary>
    Tuesday = 2,

    /// <summary>Wednesday.</summary>
    Wednesday = 4,

    /// <summary>Thursday.</summary>
    Thursday = 8,

    /// <summary>Friday.</summary>
    Friday = 16,

    /// <summary>Saturday.</summary>
    Saturday = 32,

    /// <summary>Sunday.</summary>
    Sunday = 64,

    /// <summary>All seven days.</summary>
    All = 127,
}
