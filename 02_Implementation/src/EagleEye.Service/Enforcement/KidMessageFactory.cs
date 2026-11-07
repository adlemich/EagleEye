using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Builds the messages for the kid dialog of blocked starts and enforced closes (US-006 AC-34, Decision 9; warnings and
/// grace periods go to the overlay since US-010): the reason, the parent's text for it or <c>null</c>
/// (the tray renders the default in the kid's language, ADR-016), and the values of the info line. Pure.
/// </summary>
public static class KidMessageFactory
{
    private const double SecondsPerMinute = 60;

    /// <summary>A break time is in effect: the break display text.</summary>
    public static KidMessageDto BreakTime(string? displayText, BreakTimeEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new KidMessageDto(KidMessageReason.BreakTime, displayText, null, entry.StartMinute, entry.EndMinute);
    }

    /// <summary>The app's budget is used up or the app is not allowed today: the budget text.</summary>
    /// <param name="reason"><see cref="CloseReasons.BudgetUsedUp"/> or <see cref="CloseReasons.NotAllowedToday"/>.</param>
    /// <param name="budgetText">The account's budget text, or <c>null</c> for the default.</param>
    /// <param name="appName">The app's display name.</param>
    public static KidMessageDto AppEnd(string reason, string? budgetText, string appName)
    {
        ArgumentNullException.ThrowIfNull(appName);
        var kidReason = reason == CloseReasons.BudgetUsedUp ? KidMessageReason.AppBudgetUsedUp : KidMessageReason.AppNotAllowedToday;
        return new KidMessageDto(kidReason, budgetText, appName, null, null);
    }

    /// <summary>Minutes left for the log: rounded up, at least 1.</summary>
    public static int Minutes(double secondsLeft) => Math.Max(1, (int)Math.Ceiling(secondsLeft / SecondsPerMinute));
}
