using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// State owner of the areas "AppRules:{sid}" (ADR-010 §9, US-006 Decision 1): the app records of every standard account
/// with their app rules, and the budget texts. Field-level writes validated against the stored values; every write
/// takes the client's <c>requestId</c> and the sender's device name (for the log) and throws
/// <see cref="UserAccounts.UnknownAccountException"/> if the SID is not a standard account of the inventory.
/// </summary>
public interface IAppRuleService
{
    /// <summary>The current app rules of all accounts; read lock-free by the start gate and the time-limit engine.</summary>
    AppRulesSnapshot Current { get; }

    /// <summary>Loads all stored app records, rules and budget texts (service start, before the accounting loop runs, AC-36).</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>The snapshot of one account.</summary>
    Task<AppRulesDto> GetAsync(string accountSid, CancellationToken ct = default);

    /// <summary>Sets the hours or the minutes of an app's budget, combined with the stored other part (AC-8, AC-17).</summary>
    /// <exception cref="AppNotFoundException">The account has no such app record.</exception>
    Task<StateWriteAckDto> SetBudgetAsync(
        Guid requestId, string accountSid, long appId, AppBudgetPart part, int value, string deviceName, CancellationToken ct = default);

    /// <summary>Ticks or unticks a weekday of an app rule; every combination is valid (AC-7).</summary>
    /// <exception cref="AppNotFoundException">The account has no such app record.</exception>
    Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long appId, DayOfWeek day, bool isAllowed, string deviceName, CancellationToken ct = default);

    /// <summary>Sets the budget text; empty, white space only or a default text of any language restores the default (AC-10, ADR-016).</summary>
    Task<StateWriteAckDto> SetBudgetTextAsync(Guid requestId, string accountSid, string text, string deviceName, CancellationToken ct = default);
}

/// <summary>Notified by the owner of <c>AppRecords</c> (<c>UsageService</c>) when an app record is created or renamed (US-006 Decision 3).</summary>
public interface IAppRecordObserver
{
    /// <summary>An app record was created, or seen again (possibly with a new display name). Never throws for unchanged records.</summary>
    Task OnAppRecordAsync(string accountSid, long appId, string programPath, string displayName, CancellationToken ct = default);
}
