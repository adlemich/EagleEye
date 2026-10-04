using EagleEye.Shared.Communication;
using Microsoft.AspNetCore.SignalR.Client;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// Reconnect policy of a paired connection, following <see cref="ReconnectSchedule"/>: 0 s, 2 s,
/// 10 s, then every 30 s forever (US-002 AC-22).
/// </summary>
internal sealed class ParentReconnectPolicy : IRetryPolicy
{
    /// <inheritdoc />
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        ArgumentNullException.ThrowIfNull(retryContext);
        return ReconnectSchedule.GetDelay(retryContext.PreviousRetryCount);
    }
}
