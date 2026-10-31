namespace EagleEye.Shared.Models;

/// <summary>The group rule of one group for one account (US-008 AC-15). No stored rule = 0:00, no day (AC-7).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="BudgetMinutes">0 … 1439 (23:59).</param>
/// <param name="Days">Weekdays on which the group may be used.</param>
public sealed record AppGroupRuleDto(long GroupId, int BudgetMinutes, BreakTimeDays Days);
