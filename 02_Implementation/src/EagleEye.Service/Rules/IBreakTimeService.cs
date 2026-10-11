using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// State owner of the areas "AccountRules:{sid}" (ADR-010 §9, US-005 Decision 1): break-time entries and display
/// texts of all standard accounts, with field-level writes validated against the stored values. Every write method
/// takes the client's <c>requestId</c> and the device name of the sender (for the log) and throws
/// <see cref="UserAccounts.UnknownAccountException"/> if the SID is not a standard account of the inventory.
/// </summary>
public interface IBreakTimeService
{
    /// <summary>The current rules of all accounts; read lock-free by the break-time gate.</summary>
    RulesSnapshot Current { get; }

    /// <summary>Loads all stored rules (called at service start, before the accounting loop runs, AC-27).</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>The snapshot of one account.</summary>
    Task<AccountRulesDto> GetAsync(string accountSid, CancellationToken ct = default);

    /// <summary>Adds an entry with the defaults of AC-7 at the end.</summary>
    /// <exception cref="TooManyEntriesException">The account already has the maximum number of entries.</exception>
    Task<StateWriteAckDto> AddEntryAsync(Guid requestId, string accountSid, string deviceName, CancellationToken ct = default);

    /// <summary>Deletes an entry.</summary>
    /// <exception cref="EntryNotFoundException">The entry does not exist.</exception>
    Task<StateWriteAckDto> DeleteEntryAsync(Guid requestId, string accountSid, long entryId, string deviceName, CancellationToken ct = default);

    /// <summary>Switches an entry on or off.</summary>
    /// <exception cref="EntryNotFoundException">The entry does not exist.</exception>
    Task<StateWriteAckDto> SetActiveAsync(Guid requestId, string accountSid, long entryId, bool isActive, string deviceName, CancellationToken ct = default);

    /// <summary>Sets the start or end minute, validated against the stored other boundary.</summary>
    /// <exception cref="EntryNotFoundException">The entry does not exist.</exception>
    /// <exception cref="RuleValidationException">The end would not be later than the start.</exception>
    Task<StateWriteAckDto> SetTimeAsync(
        Guid requestId, string accountSid, long entryId, BreakTimeBoundary boundary, int minute, string deviceName, CancellationToken ct = default);

    /// <summary>Ticks or unticks a weekday; at least one must stay ticked.</summary>
    /// <exception cref="EntryNotFoundException">The entry does not exist.</exception>
    /// <exception cref="RuleValidationException">No day would stay ticked.</exception>
    Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long entryId, DayOfWeek day, bool isSelected, string deviceName, CancellationToken ct = default);

    /// <summary>Sets the display text; empty or white space only (or the default text) restores the default.</summary>
    Task<StateWriteAckDto> SetDisplayTextAsync(Guid requestId, string accountSid, string text, string deviceName, CancellationToken ct = default);
}
