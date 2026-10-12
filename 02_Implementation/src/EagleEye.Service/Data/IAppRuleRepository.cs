using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>Data access for <c>AppRules</c> and <c>AccountBudgetTexts</c> (US-006, migration 5).</summary>
public interface IAppRuleRepository
{
    /// <summary>Loads every app record with its rule (no rule row = default rule) and every changed budget text.</summary>
    Task<StoredAppRules> LoadAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the rule of an app record of the account. A rule equal to the default (0:00, no day) deletes the row
    /// (deny by default = no row, ADR-015 §8). Returns <c>false</c> if the account has no such app record.
    /// </summary>
    Task<bool> SetRuleAsync(string accountSid, long appId, int budgetMinutes, BreakTimeDays days, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Stores the budget text of the account; <c>null</c> deletes it (default text, ADR-016).</summary>
    Task SetBudgetTextAsync(string accountSid, string? text, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Deletes the budget texts of all accounts that are not in <paramref name="existingSids"/>; returns their SIDs.</summary>
    Task<IReadOnlyList<string>> PurgeBudgetTextsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}

/// <summary>Data access for the single row of <c>WarningSettings</c> (US-006, migration 5).</summary>
public interface IWarningSettingsRepository
{
    /// <summary>Loads the stored settings, or <c>null</c> if the parent never changed them (defaults).</summary>
    Task<StoredWarningSettings?> LoadAsync(CancellationToken ct = default);

    /// <summary>Stores the settings (insert or replace the single row).</summary>
    Task SaveAsync(StoredWarningSettings settings, DateTimeOffset nowUtc, CancellationToken ct = default);
}
