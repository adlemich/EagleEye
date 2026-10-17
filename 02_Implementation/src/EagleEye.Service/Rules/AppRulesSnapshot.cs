using System.Collections.Immutable;
using EagleEye.Service.Data;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>One app record of an account with its app rule (no stored rule = 0:00, no day; US-006 AC-5).</summary>
/// <param name="AppId">The app record.</param>
/// <param name="ProgramPath">The program path (the app's identity per account, case-insensitive).</param>
/// <param name="DisplayName">The display name, as on Reports.</param>
/// <param name="BudgetMinutes">The daily budget, 0 … 1439.</param>
/// <param name="Days">The weekdays on which the app may be used.</param>
public sealed record AppRuleEntry(long AppId, string ProgramPath, string DisplayName, int BudgetMinutes, BreakTimeDays Days)
{
    /// <summary>Whether the app may be used on <paramref name="day"/> (day ticked and budget greater than 0:00).</summary>
    public bool IsAllowedOn(DayOfWeek day) => AppRuleRules.IsAllowedOn(BudgetMinutes, Days, day);
}

/// <summary>The app rules of one account: its app records by id and by program path, and its budget text.</summary>
public sealed class AccountAppRules
{
    private AccountAppRules(ImmutableDictionary<long, AppRuleEntry> apps, ImmutableDictionary<string, AppRuleEntry> byPath, string? budgetText)
    {
        Apps = apps;
        ByPath = byPath;
        BudgetText = budgetText;
    }

    /// <summary>No app records and the default budget text.</summary>
    public static AccountAppRules Empty { get; } = new(
        ImmutableDictionary<long, AppRuleEntry>.Empty, ImmutableDictionary.Create<string, AppRuleEntry>(StringComparer.OrdinalIgnoreCase), null);

    /// <summary>The app records by id.</summary>
    public ImmutableDictionary<long, AppRuleEntry> Apps { get; }

    /// <summary>The app records by program path (case-insensitive).</summary>
    public ImmutableDictionary<string, AppRuleEntry> ByPath { get; }

    /// <summary>The parent's budget text, or <c>null</c> for the default (ADR-016).</summary>
    public string? BudgetText { get; }

    /// <summary>Whether nothing is stored for the account.</summary>
    public bool IsEmpty => Apps.IsEmpty && BudgetText is null;

    /// <summary>A copy with the app record added or replaced.</summary>
    public AccountAppRules WithApp(AppRuleEntry app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var byPath = Apps.TryGetValue(app.AppId, out var old) ? ByPath.Remove(old.ProgramPath) : ByPath;
        return new AccountAppRules(Apps.SetItem(app.AppId, app), byPath.SetItem(app.ProgramPath, app), BudgetText);
    }

    /// <summary>A copy with another budget text.</summary>
    public AccountAppRules WithBudgetText(string? budgetText) => new(Apps, ByPath, budgetText);
}

/// <summary>
/// Immutable copy of the app rules of all accounts (US-006 Decision 1). Replaced as a whole on every change and read
/// lock-free by the start gate and the time-limit engine. Keyed by SID (case-insensitive). Programs of the enforcement
/// ignore list (File Explorer, shell hosts) are never part of it (AC-3).
/// </summary>
public sealed class AppRulesSnapshot
{
    private readonly ImmutableDictionary<string, AccountAppRules> _accounts;

    private AppRulesSnapshot(ImmutableDictionary<string, AccountAppRules> accounts)
    {
        _accounts = accounts;
    }

    /// <summary>No app rules at all.</summary>
    public static AppRulesSnapshot Empty { get; } = new(ImmutableDictionary.Create<string, AccountAppRules>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The SIDs that have app records or a budget text.</summary>
    public IEnumerable<string> AccountSids => _accounts.Keys;

    /// <summary>Builds the snapshot from the stored rows; records of ignored programs are left out.</summary>
    public static AppRulesSnapshot From(StoredAppRules stored, Func<string, bool> isIgnored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(isIgnored);
        var snapshot = Empty;
        foreach (var app in stored.Apps)
        {
            if (isIgnored(app.ProgramPath))
            {
                continue;
            }

            snapshot = snapshot.With(
                app.AccountSid,
                snapshot.For(app.AccountSid).WithApp(new AppRuleEntry(app.AppId, app.ProgramPath, app.DisplayName, app.BudgetMinutes, app.Days)));
        }

        foreach (var text in stored.BudgetTexts)
        {
            snapshot = snapshot.With(text.AccountSid, snapshot.For(text.AccountSid).WithBudgetText(text.Text));
        }

        return snapshot;
    }

    /// <summary>The app rules of the account; <see cref="AccountAppRules.Empty"/> if nothing is stored.</summary>
    public AccountAppRules For(string accountSid)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        return _accounts.TryGetValue(accountSid, out var rules) ? rules : AccountAppRules.Empty;
    }

    /// <summary>The app record of the account's program path, or <c>null</c> (then the default rule applies).</summary>
    public AppRuleEntry? RuleFor(string accountSid, string programPath)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return For(accountSid).ByPath.GetValueOrDefault(programPath);
    }

    /// <summary>A copy with the account's rules replaced (removed if empty).</summary>
    public AppRulesSnapshot With(string accountSid, AccountAppRules rules)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(rules);
        return new AppRulesSnapshot(rules.IsEmpty ? _accounts.Remove(accountSid) : _accounts.SetItem(accountSid, rules));
    }

    /// <summary>A copy without the accounts.</summary>
    public AppRulesSnapshot Without(IEnumerable<string> accountSids)
    {
        ArgumentNullException.ThrowIfNull(accountSids);
        return new AppRulesSnapshot(_accounts.RemoveRange(accountSids));
    }
}
