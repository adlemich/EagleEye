using System.Net;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// SignalR hub for tray clients running in user sessions on the same machine.
/// Thin routing layer: delegates to injected services and logs the connection lifecycle.
/// Rejects connections from non-loopback addresses (ADR-008 §1, defence in depth) and counts
/// the open tray connections for the pairing-code notifier. US-005 (ADR-014 §1): the service identifies the process
/// behind each connection itself and registers the genuine tray client with its session; unverified connections keep
/// working (version, pairing codes) but never receive kid messages.
/// </summary>
public sealed class TrayHub(
    IVersionProvider versionProvider,
    ITrayConnectionTracker connectionTracker,
    ITrayClientIdentifier identifier,
    TrayConnectionRegistry registry,
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
        var connection = Context.GetHttpContext()?.Connection;
        var remoteAddress = connection?.RemoteIpAddress;
        if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress))
        {
            logger.LogWarning("Tray connection from a non-loopback address rejected: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return Task.CompletedTask;
        }

        connectionTracker.Increment();
        Context.Items[TrackedKey] = true;
        if (Identify(connection!) is { } identity) // Not null: the remote address was read from it.
        {
            registry.Register(Context.ConnectionId, identity);
            logger.LogInformation("Tray client connected: {ConnectionId} (session {SessionId}, verified).", Context.ConnectionId, identity.SessionId);
        }
        else
        {
            logger.LogInformation("Tray client connected: {ConnectionId} (unverified).", Context.ConnectionId);
        }

        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.Remove(TrackedKey))
        {
            connectionTracker.Decrement();
        }

        registry.Unregister(Context.ConnectionId);
        logger.LogInformation(exception, "Tray client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private TrayClientIdentity? Identify(Microsoft.AspNetCore.Http.ConnectionInfo connection)
    {
        if (connection.LocalIpAddress is not { } localAddress || connection.RemoteIpAddress is not { } remoteAddress)
        {
            return null;
        }

        try
        {
            return identifier.Identify(new IPEndPoint(localAddress, connection.LocalPort), new IPEndPoint(remoteAddress, connection.RemotePort));
        }
        catch (Exception ex)
        {
            // Win32 boundary: an unidentified tray client still works, it only gets no kid messages.
            logger.LogWarning(ex, "The tray client behind connection {ConnectionId} could not be identified.", Context.ConnectionId);
            return null;
        }
    }
}
