using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>A daily budget with allowed weekdays: an app's own rule or a group rule (ADR-021 §4).</summary>
/// <param name="BudgetMinutes">The daily budget, 0 … 1439.</param>
/// <param name="Days">The weekdays on which the subject may be used.</param>
public sealed record LimitRule(int BudgetMinutes, BreakTimeDays Days)
{
    /// <summary>The default rule (0:00, no day): blocked (deny by default).</summary>
    public static LimitRule Default { get; } = new(AppRuleRules.DefaultBudgetMinutes, AppRuleRules.DefaultDays);

    /// <summary>Whether the subject may be used on <paramref name="day"/> (day ticked and budget greater than 0:00).</summary>
    public bool IsAllowedOn(DayOfWeek day) => AppRuleRules.IsAllowedOn(BudgetMinutes, Days, day);
}
