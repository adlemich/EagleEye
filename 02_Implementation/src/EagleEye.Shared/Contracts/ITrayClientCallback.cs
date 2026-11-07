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
    /// Shows a message in the kid dialog for a blocked start or an enforced close (FR-TRAY-021, FR-TRAY-022, ADR-014 as amended
    /// by US-006 and US-010; warnings and grace periods use <see cref="OnRemainingTime"/>). Sent to one verified
    /// tray connection of the session as a request with a result (SignalR client results). The tray answers at once,
    /// without waiting for "OK". If a dialog is open, its text is replaced (AC-34). The tray renders a default text
    /// (<see cref="KidMessageDto.ParentText"/> = null) and the info line in the kid's language (ADR-016).
    /// </summary>
    /// <param name="message">The message to show.</param>
    Task<KidMessageResult> ShowKidMessage(KidMessageDto message);

    /// <summary>
    /// The remaining-time model of the session (FR-TRAY-020, FR-TRAY-030; ADR-014 as amended by US-010, ADR-022). Fire-and-forget,
    /// sent every engine tick (5 s) to the newest verified tray connection of a session of a controlled account. The tray counts
    /// down locally between pushes and hides the overlay when no push came for 15 s.
    /// </summary>
    /// <param name="model">The lines, level minutes and signal of this tick.</param>
    Task OnRemainingTime(RemainingTimeDto model);
}
