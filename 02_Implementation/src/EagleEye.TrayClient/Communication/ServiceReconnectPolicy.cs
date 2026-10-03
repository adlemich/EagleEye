using Microsoft.AspNetCore.SignalR.Client;

namespace EagleEye.TrayClient.Communication;

/// <summary>
/// Reconnect policy for a lost connection: retries after 0 s, 2 s and 10 s, then every 30 s
/// forever. The SignalR default policy gives up after four attempts, but the tray client
/// must keep trying for as long as it runs (FR-TRAY-042).
/// </summary>
internal sealed class ServiceReconnectPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] InitialDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
    ];

    private static readonly TimeSpan SteadyDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        ArgumentNullException.ThrowIfNull(retryContext);

        return retryContext.PreviousRetryCount < InitialDelays.Length
            ? InitialDelays[retryContext.PreviousRetryCount]
            : SteadyDelay;
    }
}
