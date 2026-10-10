using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Sends the break-time message to the newest verified tray connection of a session as a request with a result
/// (SignalR client results, ADR-014 §2) and maps the outcome to the message state. Waits at most
/// <see cref="AnswerTimeout"/>; never throws (enforcement never depends on the tray client, NFR-R-012).
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
    public async Task<string> ShowAsync(int sessionId, string displayText, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(displayText);
        if (registry.NewestOf(sessionId) is not { } connectionId)
        {
            return KidMessageStates.NotConnected;
        }

        using var timeout = new CancellationTokenSource(AnswerTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);
        try
        {
            var result = await trayHub.Clients.Client(connectionId)
                .InvokeAsync<KidMessageResult>(nameof(ITrayClientCallback.ShowBreakTimeMessage), new BreakTimeMessageDto(displayText), linked.Token)
                .ConfigureAwait(false);
            return result == KidMessageResult.Shown ? KidMessageStates.Shown : KidMessageStates.AlreadyOpen;
        }
        catch (Exception ex)
        {
            // Tray boundary: timeout, disconnect, an old tray client without the handler, or any other failure.
            logger.LogWarning(ex, "The tray client of session {SessionId} did not answer the break-time message.", sessionId);
            return KidMessageStates.NoAnswer;
        }
    }
}
