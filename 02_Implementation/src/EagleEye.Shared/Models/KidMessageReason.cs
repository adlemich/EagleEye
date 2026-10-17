namespace EagleEye.Shared.Models;

/// <summary>Why the kid dialog is shown; decides the default text and the info line (US-006 AC-34, ADR-014 amendment).</summary>
public enum KidMessageReason
{
    /// <summary>Early warning before a budget, not-allowed or midnight end. Text: early warning; info: "{App}: {n} minutes left."</summary>
    AppEarlyWarning,

    /// <summary>Final warning before a budget, not-allowed or midnight end. Text: final warning; info: "{App}: {n} minutes left."</summary>
    AppFinalWarning,

    /// <summary>Early warning before a break begins. Text: early warning; info: "Break time begins at {start}."</summary>
    BreakEarlyWarning,

    /// <summary>Final warning before a break begins. Text: final warning; info: "Break time begins at {start}."</summary>
    BreakFinalWarning,

    /// <summary>Grace period after an app rule change. Text: final warning; info: "{App}: your parents changed the rules …".</summary>
    AppGracePeriod,

    /// <summary>Grace period after a break-time change. Text: final warning; info: "Your parents set a break time …".</summary>
    BreakGracePeriod,

    /// <summary>The app is not allowed today (or at midnight). Text: budget text; info: "{App}: not allowed today."</summary>
    AppNotAllowedToday,

    /// <summary>The app's budget is used up. Text: budget text; info: "{App}: time budget used up."</summary>
    AppBudgetUsedUp,

    /// <summary>A break time is in effect. Text: break display text; info: "Break time from {start} to {end}."</summary>
    BreakTime,
}
