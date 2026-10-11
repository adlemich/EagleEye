using EagleEye.ParentApp.Core.Accounts;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of the state area "AccountRules:{sid}" (US-005, ADR-010) for the one account selected on the Rules
/// page. Every write returns <c>true</c> when a snapshot confirming it was applied, and <c>false</c> on rejection,
/// connection loss or timeout (then the stored state is fetched again if the outcome is unknown).
/// </summary>
public interface IAccountRulesModel
{
    /// <summary>Raised (on any thread) when the selection, the load state or the snapshot changed.</summary>
    event Action? Changed;

    /// <summary>The selected account's SID, or null.</summary>
    string? SelectedAccountSid { get; }

    /// <summary>Whether the rules of the selected account are available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot of the selected account, or null.</summary>
    AccountRulesDto? Snapshot { get; }

    /// <summary>Selects an account (or none) and fetches its rules while connected (AC-5).</summary>
    void SelectAccount(string? accountSid);

    /// <summary>Adds an entry with the defaults (AC-7).</summary>
    Task<bool> AddEntryAsync();

    /// <summary>Deletes an entry (AC-13).</summary>
    Task<bool> DeleteEntryAsync(long entryId);

    /// <summary>Switches an entry on or off (AC-14).</summary>
    Task<bool> SetActiveAsync(long entryId, bool isActive);

    /// <summary>Sets a start or end time (AC-9, AC-10).</summary>
    Task<bool> SetTimeAsync(long entryId, BreakTimeBoundary boundary, int minute);

    /// <summary>Ticks or unticks a weekday (AC-11).</summary>
    Task<bool> SetDayAsync(long entryId, DayOfWeek day, bool isSelected);

    /// <summary>Sets the display text; empty restores the default (AC-15, OQ-8).</summary>
    Task<bool> SetDisplayTextAsync(string text);
}
