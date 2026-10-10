using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// One SignalR connection to the service's parent hub (<c>IParentHub</c>). Events are raised on
/// thread-pool threads.
/// </summary>
public interface IParentHubClient : IAsyncDisposable
{
    /// <summary>The connection was lost and automatic reconnecting started.</summary>
    event Action? Reconnecting;

    /// <summary>The connection was re-established after <see cref="Reconnecting"/>.</summary>
    event Action? Reconnected;

    /// <summary>The connection is closed for good (no further automatic reconnect).</summary>
    event Action? Closed;

    /// <summary>Opens the connection.</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Calls <c>IParentHub.GetPairingStatus</c>.</summary>
    Task<PairingStatusDto> GetPairingStatusAsync(CancellationToken ct);

    /// <summary>Calls <c>IParentHub.StartPairing</c>.</summary>
    Task StartPairingAsync(CancellationToken ct);

    /// <summary>Calls <c>IParentHub.SubmitPairingCode</c>.</summary>
    Task<PairingResultDto> SubmitPairingCodeAsync(string code, string deviceName, CancellationToken ct);

    /// <summary>Calls <c>IParentHub.RemovePairedDevice</c>.</summary>
    Task RemovePairedDeviceAsync(string deviceId, CancellationToken ct);

    /// <summary>The service broadcast <c>IParentClientCallback.OnUserAccountsChanged</c> (ADR-010).</summary>
    event Action<UserAccountListDto>? UserAccountsChanged;

    /// <summary>Calls <c>IParentHub.GetUserAccounts</c>.</summary>
    Task<UserAccountListDto> GetUserAccountsAsync(CancellationToken ct);

    /// <summary>Calls <c>IParentHub.SetParentalControl</c>.</summary>
    Task<StateWriteAckDto> SetParentalControlAsync(Guid requestId, string accountSid, bool isUnderParentalControl, CancellationToken ct);

    /// <summary>The service broadcast <c>IParentClientCallback.OnDayUsageChanged</c> (ADR-012 §6).</summary>
    event Action<DayUsageDto>? DayUsageChanged;

    /// <summary>Calls <c>IParentHub.GetAccountUsage</c>.</summary>
    Task<AccountUsageDto> GetAccountUsageAsync(string accountSid, CancellationToken ct);
}
