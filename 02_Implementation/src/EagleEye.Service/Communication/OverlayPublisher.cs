using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Sends <see cref="ITrayClientCallback.OnRemainingTime"/> to the newest verified tray connection of a session (ADR-014 §1
/// binding, US-010 Decision 1). Fire-and-forget: the engine never waits; a failed send is logged at Debug level (every 5 s
/// a new model follows). Enforcement never depends on the tray client (NFR-R-012).
/// </summary>
public sealed class OverlayPublisher(
    TrayConnectionRegistry registry,
    IHubContext<TrayHub> trayHub,
    ILogger<OverlayPublisher> logger) : IOverlayPublisher
{
    /// <inheritdoc />
    public string Publish(int sessionId, RemainingTimeDto model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (registry.NewestOf(sessionId) is not { } connectionId)
        {
            return KidMessageStates.NotConnected;
        }

        try
        {
            _ = SendAsync(trayHub.Clients.Client(connectionId), sessionId, model);
        }
        catch (Exception ex)
        {
            // Tray boundary: a failure to even start the send must not reach the engine.
            logger.LogDebug(ex, "Sending the remaining time to the tray client of session {SessionId} failed.", sessionId);
        }

        return KidMessageStates.ShownInOverlay;
    }

    private async Task SendAsync(IClientProxy client, int sessionId, RemainingTimeDto model)
    {
        try
        {
            await client.SendAsync(nameof(ITrayClientCallback.OnRemainingTime), model).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Tray boundary: disconnect or any other failure; the next tick sends a new model.
            logger.LogDebug(ex, "Sending the remaining time to the tray client of session {SessionId} failed.", sessionId);
        }
    }
}
