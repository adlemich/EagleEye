using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Builds the messages for the kid dialog (US-006 AC-34, Decision 9): the reason, the parent's text for it or <c>null</c>
/// (the tray renders the default in the kid's language, ADR-016), and the values of the info line. Pure.
/// </summary>
public static class KidMessageFactory
{
    private const double SecondsPerMinute = 60;

    /// <summary>A break time is in effect: the break display text.</summary>
    public static KidMessageDto BreakTime(string? displayText, BreakTimeEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new KidMessageDto(KidMessageReason.BreakTime, displayText, null, null, entry.StartMinute, entry.EndMinute);
    }

    /// <summary>The app's budget is used up or the app is not allowed today: the budget text.</summary>
    /// <param name="reason"><see cref="CloseReasons.BudgetUsedUp"/> or <see cref="CloseReasons.NotAllowedToday"/>.</param>
    /// <param name="budgetText">The account's budget text, or <c>null</c> for the default.</param>
    /// <param name="appName">The app's display name.</param>
    public static KidMessageDto AppEnd(string reason, string? budgetText, string appName)
    {
        ArgumentNullException.ThrowIfNull(appName);
        var kidReason = reason == CloseReasons.BudgetUsedUp ? KidMessageReason.AppBudgetUsedUp : KidMessageReason.AppNotAllowedToday;
        return new KidMessageDto(kidReason, budgetText, appName, null, null, null);
    }

    /// <summary>A warning before an app's budget, not-allowed or midnight end.</summary>
    public static KidMessageDto AppWarning(WarningLevel level, WarningSettings settings, string appName, double secondsLeft)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appName);
        var reason = level == WarningLevel.Early ? KidMessageReason.AppEarlyWarning : KidMessageReason.AppFinalWarning;
        return new KidMessageDto(reason, settings.TextOf(level), appName, Minutes(secondsLeft), null, null);
    }

    /// <summary>A warning before a break begins.</summary>
    public static KidMessageDto BreakWarning(WarningLevel level, WarningSettings settings, int breakStartMinute, double secondsLeft)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var reason = level == WarningLevel.Early ? KidMessageReason.BreakEarlyWarning : KidMessageReason.BreakFinalWarning;
        return new KidMessageDto(reason, settings.TextOf(level), null, Minutes(secondsLeft), breakStartMinute, null);
    }

    /// <summary>A grace period after an app rule change: final warning text, the full final minutes (AC-32, AC-34).</summary>
    public static KidMessageDto AppGrace(WarningSettings settings, string appName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appName);
        return new KidMessageDto(KidMessageReason.AppGracePeriod, settings.FinalText, appName, settings.FinalMinutes, null, null);
    }

    /// <summary>A grace period after a break-time change: final warning text, the full final minutes (AC-33, AC-34).</summary>
    public static KidMessageDto BreakGrace(WarningSettings settings, int breakStartMinute)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new KidMessageDto(KidMessageReason.BreakGracePeriod, settings.FinalText, null, settings.FinalMinutes, breakStartMinute, null);
    }

    /// <summary>Minutes left for the info line: rounded up, at least 1.</summary>
    public static int Minutes(double secondsLeft) => Math.Max(1, (int)Math.Ceiling(secondsLeft / SecondsPerMinute));
}
