using System.Net;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// SignalR hub for tray clients running in user sessions on the same machine.
/// Thin routing layer: delegates to injected services and logs the connection lifecycle.
/// Rejects connections from non-loopback addresses (ADR-008 §1, defence in depth) and counts
/// the open tray connections for the pairing-code notifier.
/// </summary>
public sealed class TrayHub(
    IVersionProvider versionProvider,
    ITrayConnectionTracker connectionTracker,
    ILogger<TrayHub> logger) : Hub<ITrayClientCallback>, ITrayHub
{
    private const string TrackedKey = "EagleEye.TrayTracked";

    /// <inheritdoc />
    public Task<ServiceVersionDto> GetServiceVersion()
    {
        return Task.FromResult(versionProvider.GetVersion());
    }

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        var remoteAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress;
        if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress))
        {
            logger.LogWarning("Tray connection from a non-loopback address rejected: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return Task.CompletedTask;
        }

        connectionTracker.Increment();
        Context.Items[TrackedKey] = true;
        logger.LogInformation("Tray client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.Remove(TrackedKey))
        {
            connectionTracker.Decrement();
        }

        logger.LogInformation(exception, "Tray client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
