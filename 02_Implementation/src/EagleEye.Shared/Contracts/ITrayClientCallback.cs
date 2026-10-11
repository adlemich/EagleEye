using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Shared.Contracts;

/// <summary>
/// Client-side callback interface for tray clients.
/// The service invokes these methods on connected tray clients.
/// </summary>
public interface ITrayClientCallback
{
    /// <summary>
    /// Shows a pairing code to the user at the service PC (FR-SVC-091, FR-TRAY-070).
    /// The code is valid for <see cref="PairingRules.CodeLifetime"/>.
    /// </summary>
    /// <param name="code">The 6-digit pairing code.</param>
    Task OnShowPairingCode(string code);

    /// <summary>
    /// Shows the account's display text after a blocked start (FR-TRAY-022, ADR-014). Sent to one verified
    /// tray connection of the session as a request with a result (SignalR client results). The tray answers
    /// at once, without waiting for "OK".
    /// </summary>
    /// <param name="message">The text to show.</param>
    Task<KidMessageResult> ShowBreakTimeMessage(BreakTimeMessageDto message);
}
