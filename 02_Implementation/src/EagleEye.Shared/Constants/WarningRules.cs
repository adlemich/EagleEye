namespace EagleEye.Shared.Constants;

/// <summary>
/// Rules of the two global time-elapsed warnings (US-006 AC-14, AC-15, FR-APP-054). Both levels are minutes before
/// the end, 1 … 59, and the early warning must be greater than the final warning. Neither can be switched off.
/// </summary>
public static class WarningRules
{
    /// <summary>The smallest number of minutes of a level.</summary>
    public const int MinMinutes = 1;

    /// <summary>The largest number of minutes of a level.</summary>
    public const int MaxMinutes = 59;

    /// <summary>Default minutes of the early warning (AC-14).</summary>
    public const int DefaultEarlyMinutes = 5;

    /// <summary>Default minutes of the final warning (AC-14).</summary>
    public const int DefaultFinalMinutes = 1;

    /// <summary>Whether <paramref name="minutes"/> is a valid level (1 … 59).</summary>
    public static bool IsValidMinutes(int minutes)
    {
        return minutes is >= MinMinutes and <= MaxMinutes;
    }

    /// <summary>Whether both levels are valid and the early warning is greater than the final warning (AC-15).</summary>
    public static bool IsValidPair(int earlyMinutes, int finalMinutes)
    {
        return IsValidMinutes(earlyMinutes) && IsValidMinutes(finalMinutes) && earlyMinutes > finalMinutes;
    }
}
