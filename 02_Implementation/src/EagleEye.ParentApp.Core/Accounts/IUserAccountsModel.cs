using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Accounts;

/// <summary>Client side of the state area "UserAccounts" (ADR-010), without UI.</summary>
public interface IUserAccountsModel
{
    /// <summary>Raised (on any thread) when <see cref="LoadState"/> or <see cref="Snapshot"/> changed.</summary>
    event Action? Changed;

    /// <summary>Whether the list is available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot of the service, or <c>null</c>.</summary>
    UserAccountListDto? Snapshot { get; }

    /// <summary>
    /// Sends the selection to the service and waits for its confirmation (ADR-010 §5). Returns
    /// <c>true</c> when a snapshot confirming the write was applied, <c>false</c> on rejection,
    /// connection loss or timeout (then the stored state is fetched again).
    /// </summary>
    Task<bool> SetParentalControlAsync(string accountSid, bool isUnderParentalControl);
}
