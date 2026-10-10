using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.Communication;

/// <summary>
/// The tray client's connection to the local EagleEye service.
/// </summary>
public interface IServiceConnection : IAsyncDisposable
{
    /// <summary>Raised on every connection state change: <c>true</c> = connected, <c>false</c> = disconnected.</summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>Raised when the service sends a pairing code to show (FR-TRAY-070). Raised on a thread-pool thread.</summary>
    event Action<string>? PairingCodeReceived;

    /// <summary>
    /// Answers <c>ShowBreakTimeMessage</c> requests of the service (ADR-014 §2) with the display text; must return at
    /// once. Called on a thread-pool thread. Without a handler the request fails (the service logs "did not answer").
    /// </summary>
    Func<string, KidMessageResult>? BreakTimeMessageHandler { get; set; }

    /// <summary>Whether the connection to the service is currently established.</summary>
    bool IsConnected { get; }

    /// <summary>The address (host:port) of the service this connection targets, e.g. "localhost:5080".</summary>
    string ServerAddress { get; }

    /// <summary>
    /// Connects to the service, retrying until it succeeds or <paramref name="ct"/> is cancelled.
    /// </summary>
    Task ConnectAsync(CancellationToken ct);

    /// <summary>Queries the service version live from the service.</summary>
    Task<ServiceVersionDto> GetServiceVersionAsync(CancellationToken ct);
}
