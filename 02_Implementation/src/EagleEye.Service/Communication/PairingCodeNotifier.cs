using EagleEye.Service.Diagnostics;
using EagleEye.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Pushes a pairing code to all connected tray clients, or writes it to the Event Log when no
/// tray client is connected (FR-SVC-091, FR-SVC-092, ADR-008 §6).
/// </summary>
public sealed class PairingCodeNotifier(
    ITrayConnectionTracker trayConnections,
    IHubContext<TrayHub, ITrayClientCallback> trayHub,
    IPairingCodeEventLog eventLog,
    ILogger<PairingCodeNotifier> logger) : IPairingCodeNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var trayCount = trayConnections.Count;
        if (trayCount > 0)
        {
            await trayHub.Clients.All.OnShowPairingCode(code).ConfigureAwait(false);
            logger.LogInformation("Pairing code *** sent to {TrayCount} tray client connection(s).", trayCount);
            return;
        }

        eventLog.Write(code);
    }
}
