namespace EagleEye.Shared.Models;

/// <summary>
/// Why the kid dialog is shown; decides the default text and the info line (US-006 AC-34, ADR-014 amendment). Since US-010
/// the dialog is used for blocked starts and enforced closes only; warnings and grace periods go to the overlay.
/// </summary>
public enum KidMessageReason
{
    /// <summary>The app is not allowed today (or at midnight). Text: budget text; info: "{App}: not allowed today."</summary>
    AppNotAllowedToday,

    /// <summary>The app's budget is used up. Text: budget text; info: "{App}: time budget used up."</summary>
    AppBudgetUsedUp,

    /// <summary>A break time is in effect. Text: break display text; info: "Break time from {start} to {end}."</summary>
    BreakTime,
}
