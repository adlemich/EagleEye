using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// SignalR hub for tray clients running in user sessions on the same machine.
/// Thin routing layer: delegates to injected services and logs the connection lifecycle.
/// </summary>
public sealed class TrayHub(IVersionProvider versionProvider, ILogger<TrayHub> logger)
    : Hub<ITrayClientCallback>, ITrayHub
{
    /// <inheritdoc />
    public Task<ServiceVersionDto> GetServiceVersion()
    {
        return Task.FromResult(versionProvider.GetVersion());
    }

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Tray client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(exception, "Tray client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
