using EagleEye.ParentApp.Core.Accounts;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Reports;

/// <summary>Client side of the usage areas (ADR-012 §6) for the one account selected on the Reports page.</summary>
public interface IAccountUsageModel
{
    /// <summary>Raised (on any thread) when the selection, the load state or a day changed.</summary>
    event Action? Changed;

    /// <summary>The selected account's SID, or null.</summary>
    string? SelectedAccountSid { get; }

    /// <summary>Whether the usage of the selected account is available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The service PC's local date of the newest snapshot, or null before the first one.</summary>
    DateOnly? ServiceToday { get; }

    /// <summary>The days held for the selected account, newest first (days without usage except today are dropped).</summary>
    IReadOnlyList<DayUsageDto> Days { get; }

    /// <summary>Selects an account (or none) and fetches its usage while connected (AC-21).</summary>
    void SelectAccount(string? accountSid);
}
