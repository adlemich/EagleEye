using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// The current paired connection for feature models (ADR-010, coding guidelines §7.5), so that
/// <see cref="ConnectionCoordinator"/> does not grow with every feature. Events are raised on
/// thread-pool threads.
/// </summary>
public interface IParentHubGateway
{
    /// <summary>Whether a paired connection is confirmed (connected and <c>IsPaired</c>).</summary>
    bool IsConnected { get; }

    /// <summary>Raised on every confirmed (re)connect. Feature models reset and fetch their areas.</summary>
    event Action? Connected;

    /// <summary>Raised when the confirmed connection is lost or stopped.</summary>
    event Action? Disconnected;

    /// <summary>Broadcast of the area "UserAccounts", forwarded only from the current client while connected.</summary>
    event Action<UserAccountListDto>? UserAccountsChanged;

    /// <summary>Broadcast of a usage day area (ADR-012 §6), forwarded only from the current client while connected.</summary>
    event Action<DayUsageDto>? DayUsageChanged;

    /// <summary>Broadcast of an area "AccountRules:{sid}" (US-005), forwarded only from the current client while connected.</summary>
    event Action<AccountRulesDto>? AccountRulesChanged;

    /// <summary>Runs a hub call on the current connection with a timeout.</summary>
    /// <exception cref="ParentHubNotConnectedException">There is no confirmed connection.</exception>
    Task<T> InvokeAsync<T>(Func<IParentHubClient, CancellationToken, Task<T>> call, TimeSpan timeout);
}
