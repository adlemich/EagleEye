namespace EagleEye.Shared.Models;

/// <summary>One app record of an account with its app rule (FR-APP-040). No stored rule = 0 minutes, no day (US-006 AC-5).</summary>
/// <param name="AppId">The app record (US-004).</param>
/// <param name="DisplayName">As on Reports (AC-2).</param>
/// <param name="ProgramPath">Shown as a tooltip, to tell two records with the same name apart (OQ-12).</param>
/// <param name="BudgetMinutes">0 … 1439 (23:59).</param>
/// <param name="Days">Weekdays on which the app may be used (bit mask, Monday = 1; same type as break times).</param>
public sealed record AppRuleDto(long AppId, string DisplayName, string ProgramPath, int BudgetMinutes, BreakTimeDays Days);
