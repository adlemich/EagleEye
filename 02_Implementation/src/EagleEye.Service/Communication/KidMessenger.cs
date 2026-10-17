using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Sends a kid message to the newest verified tray connection of a session as a request with a result (SignalR client
/// results, ADR-014 §2) and maps the outcome to the message state. Waits at most <see cref="AnswerTimeout"/>; never
/// throws (enforcement never depends on the tray client, NFR-R-012).
/// </summary>
public sealed class KidMessenger(
    TrayConnectionRegistry registry,
    IHubContext<TrayHub> trayHub,
    TimeProvider timeProvider,
    ILogger<KidMessenger> logger) : IKidMessenger
{
    /// <summary>How long the service waits for the tray client's answer.</summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public async Task<string> ShowAsync(int sessionId, KidMessageDto message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (registry.NewestOf(sessionId) is not { } connectionId)
        {
            return KidMessageStates.NotConnected;
        }

        using var timeout = new CancellationTokenSource(AnswerTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);
        try
        {
            var result = await trayHub.Clients.Client(connectionId)
                .InvokeAsync<KidMessageResult>(nameof(ITrayClientCallback.ShowKidMessage), message, linked.Token)
                .ConfigureAwait(false);
            return result switch
            {
                KidMessageResult.Shown => KidMessageStates.Shown,
                KidMessageResult.Replaced => KidMessageStates.Replaced,
                _ => KidMessageStates.AlreadyOpen,
            };
        }
        catch (Exception ex)
        {
            // Tray boundary: timeout, disconnect, an old tray client without the handler, or any other failure.
            logger.LogWarning(ex, "The tray client of session {SessionId} did not answer the kid message.", sessionId);
            return KidMessageStates.NoAnswer;
        }
    }
}
