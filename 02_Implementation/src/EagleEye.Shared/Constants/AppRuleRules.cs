using System.Globalization;
using EagleEye.Shared.Models;

namespace EagleEye.Shared.Constants;

/// <summary>
/// App-rule rules shared by the service and the parent app (US-006, FR-APP-040, TC-020 to TC-025, ADR-015).
/// A budget is a number of minutes per day, 0 … 1439 (23:59). An app without a stored rule has the default rule
/// (0:00, no day ticked) and is blocked (deny by default, AC-5).
/// </summary>
public static class AppRuleRules
{
    /// <summary>The largest budget: 23:59 (AC-8).</summary>
    public const int MaxBudgetMinutes = 1439;

    /// <summary>The largest value of the hours field (AC-8).</summary>
    public const int MaxHours = 23;

    /// <summary>The largest value of the minutes field (AC-8).</summary>
    public const int MaxMinutes = 59;

    /// <summary>The budget of the default rule (AC-5).</summary>
    public const int DefaultBudgetMinutes = 0;

    /// <summary>The days of the default rule (AC-5).</summary>
    public const BreakTimeDays DefaultDays = BreakTimeDays.None;

    private const int MinutesPerHour = 60;

    /// <summary>Whether <paramref name="budgetMinutes"/> is a valid budget (0 … 1439).</summary>
    public static bool IsValidBudget(int budgetMinutes)
    {
        return budgetMinutes is >= 0 and <= MaxBudgetMinutes;
    }

    /// <summary>Whether <paramref name="days"/> is a valid day mask (any combination, also none, AC-7).</summary>
    public static bool IsValidDays(BreakTimeDays days)
    {
        return (days & ~BreakTimeDays.All) == 0;
    }

    /// <summary>Whether <paramref name="value"/> is valid for the budget part (hours 0 … 23, minutes 0 … 59).</summary>
    public static bool IsValidPart(AppBudgetPart part, int value)
    {
        return part switch
        {
            AppBudgetPart.Hours => value is >= 0 and <= MaxHours,
            AppBudgetPart.Minutes => value is >= 0 and <= MaxMinutes,
            _ => false,
        };
    }

    /// <summary>
    /// Whether the app may be used on <paramref name="day"/>: the day is ticked and the budget is greater than 0:00 (AC-9, AC-19).
    /// </summary>
    public static bool IsAllowedOn(int budgetMinutes, BreakTimeDays days, DayOfWeek day)
    {
        return budgetMinutes > 0 && (days & BreakTimeRules.ToDays(day)) != 0;
    }

    /// <summary>Combines a new hours or minutes value with the other part of the stored budget (TI-13, AC-17).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The stored budget or the value is out of range.</exception>
    public static int Combine(int storedBudgetMinutes, AppBudgetPart part, int value)
    {
        if (!IsValidBudget(storedBudgetMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(storedBudgetMinutes), storedBudgetMinutes, "Invalid budget.");
        }

        if (!IsValidPart(part, value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid budget part.");
        }

        return part == AppBudgetPart.Hours
            ? (value * MinutesPerHour) + (storedBudgetMinutes % MinutesPerHour)
            : (storedBudgetMinutes / MinutesPerHour * MinutesPerHour) + value;
    }

    /// <summary>The hours part of a budget.</summary>
    public static int HoursOf(int budgetMinutes)
    {
        return budgetMinutes / MinutesPerHour;
    }

    /// <summary>The minutes part of a budget.</summary>
    public static int MinutesOf(int budgetMinutes)
    {
        return budgetMinutes % MinutesPerHour;
    }

    /// <summary>Formats a budget as "H:MM" for the log (AC-11), e.g. "0:45".</summary>
    public static string Format(int budgetMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(budgetMinutes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(budgetMinutes, MaxBudgetMinutes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{budgetMinutes / MinutesPerHour}:{budgetMinutes % MinutesPerHour:00}");
    }
}
