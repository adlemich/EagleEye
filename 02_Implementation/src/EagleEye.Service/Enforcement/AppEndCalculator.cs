using EagleEye.Service.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>What ends an app's allowed time (ADR-015 §3). The order is the tie order: break before midnight before not allowed before budget.</summary>
public enum AppEndKind
{
    /// <summary>A break time begins (or is in effect): break display text, break warnings.</summary>
    Break,

    /// <summary>Local midnight, and the next day does not allow the app: budget text, app warnings (OQ-4).</summary>
    Midnight,

    /// <summary>Today is not ticked or the budget is 0:00: budget text.</summary>
    NotAllowedToday,

    /// <summary>The budget runs out: budget text, app warnings.</summary>
    Budget,
}

/// <summary>The end of an app's allowed time.</summary>
/// <param name="Kind">What ends it.</param>
/// <param name="SecondsLeft">Seconds left: of further use for <see cref="AppEndKind.Budget"/>, of wall time otherwise; ≥ 0.</param>
/// <param name="Entry">The break entry (<see cref="AppEndKind.Break"/>), else <c>null</c>.</param>
/// <param name="BreakStartLocal">The local start of that break occurrence, else <c>null</c>.</param>
public sealed record AppEnd(AppEndKind Kind, double SecondsLeft, BreakTimeEntryDto? Entry, DateTime? BreakStartLocal);

/// <summary>
/// Pure computation of the end of a running app's allowed time (ADR-015 §3, US-006 Decision 4): the earliest of the
/// budget end (only if it falls before the next local midnight), not allowed today (0), midnight when the next day does
/// not allow the app, and the next break begin (0 when a break is in effect). Only ends within <see cref="Horizon"/> count.
/// </summary>
public static class AppEndCalculator
{
    /// <summary>Ends further away do not matter yet: warning levels are at most 59 minutes.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromMinutes(60);

    private const double SecondsPerMinute = 60;

    /// <summary>The end within the horizon, or <c>null</c>.</summary>
    /// <param name="rule">The app rule; <c>null</c> = the default rule (0:00, no day).</param>
    /// <param name="usedSeconds">Used time today.</param>
    /// <param name="entries">The account's break entries.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="zone">The local zone.</param>
    public static AppEnd? Compute(AppRuleEntry? rule, long usedSeconds, IReadOnlyList<BreakTimeEntryDto> entries, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(zone);
        var local = BreakTimeSchedule.ToLocal(nowUtc, zone);
        var budget = rule?.BudgetMinutes ?? AppRuleRules.DefaultBudgetMinutes;
        var days = rule?.Days ?? AppRuleRules.DefaultDays;
        var candidates = new List<AppEnd>(4);

        if (BreakTimeSchedule.FirstInEffect(entries, local) is { } inEffect)
        {
            candidates.Add(new AppEnd(AppEndKind.Break, 0, inEffect, local.Date.AddMinutes(inEffect.StartMinute)));
        }
        else if (BreakTimeSchedule.NextBegin(entries, nowUtc, zone, Horizon) is { } begin)
        {
            candidates.Add(new AppEnd(AppEndKind.Break, begin.Until.TotalSeconds, begin.Entry, begin.StartLocal));
        }

        var untilMidnight = (BreakTimeSchedule.FirstPassUtc(NextMidnight(local.Date, zone), zone) - nowUtc).TotalSeconds;
        if (!AppRuleRules.IsAllowedOn(budget, days, local.DayOfWeek))
        {
            candidates.Add(new AppEnd(AppEndKind.NotAllowedToday, 0, null, null));
        }
        else
        {
            var budgetLeft = Math.Max(0, (budget * SecondsPerMinute) - usedSeconds);
            if (budgetLeft < untilMidnight)
            {
                candidates.Add(new AppEnd(AppEndKind.Budget, budgetLeft, null, null));
            }

            if (!AppRuleRules.IsAllowedOn(budget, days, local.AddDays(1).DayOfWeek))
            {
                candidates.Add(new AppEnd(AppEndKind.Midnight, untilMidnight, null, null));
            }
        }

        return candidates
            .Where(c => c.SecondsLeft <= Horizon.TotalSeconds)
            .OrderBy(c => c.SecondsLeft)
            .ThenBy(c => c.Kind)
            .FirstOrDefault();
    }

    /// <summary>The next local midnight as a valid wall-clock time (a midnight inside a skipped hour moves to its end).</summary>
    private static DateTime NextMidnight(DateTime today, TimeZoneInfo zone)
    {
        var midnight = today.AddDays(1);
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(1);
        }

        return midnight;
    }
}
