using EagleEye.Shared.Communication;
using Microsoft.AspNetCore.SignalR.Client;

namespace EagleEye.TrayClient.Communication;

/// <summary>
/// Reconnect policy for a lost connection, following <see cref="ReconnectSchedule"/>: retries
/// after 0 s, 2 s and 10 s, then every 30 s forever. The SignalR default policy gives up after
/// four attempts, but the tray client must keep trying for as long as it runs (FR-TRAY-042).
/// </summary>
internal sealed class ServiceReconnectPolicy : IRetryPolicy
{
    /// <inheritdoc />
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        ArgumentNullException.ThrowIfNull(retryContext);

        return ReconnectSchedule.GetDelay(retryContext.PreviousRetryCount);
    }
}
