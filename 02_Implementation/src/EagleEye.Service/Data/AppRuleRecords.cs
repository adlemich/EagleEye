using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>An app record of an account with its stored app rule (US-006, migration 5).</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="AppId">The app record.</param>
/// <param name="ProgramPath">The program path.</param>
/// <param name="DisplayName">The display name, as on Reports.</param>
/// <param name="BudgetMinutes">The stored budget, or the default (0) when there is no rule row.</param>
/// <param name="Days">The stored days, or the default (none) when there is no rule row.</param>
public sealed record StoredAppRecord(
    string AccountSid, long AppId, string ProgramPath, string DisplayName, int BudgetMinutes, BreakTimeDays Days);

/// <summary>Everything stored for the app rules of all accounts.</summary>
/// <param name="Apps">All app records with their rules (no rule row = default rule).</param>
/// <param name="BudgetTexts">All changed budget texts (no row = default text, ADR-016).</param>
public sealed record StoredAppRules(IReadOnlyList<StoredAppRecord> Apps, IReadOnlyList<StoredDisplayText> BudgetTexts);

/// <summary>The stored warning settings (single row of <c>WarningSettings</c>); texts <c>null</c> = default text.</summary>
/// <param name="EarlyMinutes">Minutes of the early warning.</param>
/// <param name="EarlyText">The parent's early warning text, or <c>null</c>.</param>
/// <param name="FinalMinutes">Minutes of the final warning.</param>
/// <param name="FinalText">The parent's final warning text, or <c>null</c>.</param>
public sealed record StoredWarningSettings(int EarlyMinutes, string? EarlyText, int FinalMinutes, string? FinalText);
