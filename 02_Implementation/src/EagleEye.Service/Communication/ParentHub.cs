using EagleEye.Service.Pairing;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// SignalR hub for parent apps on the TLS endpoint (ADR-008). A connection that presents a known
/// token (<c>Authorization: Bearer</c>) is paired; all other connections may only pair
/// (enforced by <see cref="PairingAuthorizationHubFilter"/>). Thin routing layer: the pairing
/// logic lives in <see cref="IPairingManager"/>. Clients only see <see cref="HubException"/>s with
/// safe messages (coding guidelines §7.4).
/// </summary>
public sealed class ParentHub(
    IPairingManager pairing,
    IParentConnectionRegistry registry,
    ILogger<ParentHub> logger) : Hub<IParentClientCallback>, IParentHub
{
    /// <summary>Group of all paired parent connections.</summary>
    public const string ParentsGroup = "Parents";

    internal const string StartFailedMessage = "Pairing could not be started.";
    internal const string SubmitFailedMessage = "Pairing failed.";
    internal const string RemoveFailedMessage = "The device could not be removed.";
    internal const string UnknownDeviceMessage = "Unknown device.";

    /// <inheritdoc />
    [AllowUnpaired]
    public Task<PairingStatusDto> GetPairingStatus()
    {
        var deviceId = ParentConnectionState.GetDeviceId(Context);
        return Task.FromResult(new PairingStatusDto(deviceId is not null, deviceId, ParentConnectionState.GetDeviceName(Context)));
    }

    /// <inheritdoc />
    [AllowUnpaired]
    public async Task StartPairing()
    {
        try
        {
            await pairing.StartPairingAsync(Context.ConnectionId);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Starting pairing failed for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException(StartFailedMessage);
        }
    }

    /// <inheritdoc />
    [AllowUnpaired]
    public async Task<PairingResultDto> SubmitPairingCode(string code, string deviceName)
    {
        try
        {
            return await pairing.SubmitAsync(Context.ConnectionId, code, deviceName);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Pairing failed for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException(SubmitFailedMessage);
        }
    }

    /// <inheritdoc />
    public async Task RemovePairedDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new HubException(UnknownDeviceMessage);
        }

        bool removed;
        try
        {
            removed = await pairing.RemoveDeviceAsync(deviceId);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Removing parent device {DeviceId} failed.", deviceId);
            throw new HubException(RemoveFailedMessage);
        }

        if (!removed)
        {
            throw new HubException(UnknownDeviceMessage);
        }

        registry.AbortAll(deviceId, Context.ConnectionId);
        if (deviceId == ParentConnectionState.GetDeviceId(Context))
        {
            ParentConnectionState.Clear(Context);
            registry.Unregister(deviceId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ParentsGroup);
        }
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var token = BearerToken.Parse(Context.GetHttpContext()?.Request.Headers.Authorization.ToString());
        var device = await pairing.AuthenticateAsync(token);
        if (device is null)
        {
            logger.LogInformation("Unpaired parent app connected: {ConnectionId}.", Context.ConnectionId);
        }
        else
        {
            ParentConnectionState.SetPaired(Context, device.DeviceId, device.DeviceName);
            await Groups.AddToGroupAsync(Context.ConnectionId, ParentsGroup);
            registry.Register(device.DeviceId, Context);
            logger.LogInformation(
                "Paired parent device {DeviceId} ({DeviceName}) connected: {ConnectionId}.",
                device.DeviceId, device.DeviceName, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = ParentConnectionState.GetDeviceId(Context);
        if (deviceId is not null)
        {
            registry.Unregister(deviceId, Context.ConnectionId);
        }

        logger.LogInformation(exception, "Parent app disconnected: {ConnectionId}.", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
