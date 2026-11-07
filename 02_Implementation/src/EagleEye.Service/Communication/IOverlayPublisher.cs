using EagleEye.Shared.Models;

namespace EagleEye.Service.Communication;

/// <summary>Pushes the remaining-time model to the tray client of a session (US-010 Decision 1, ADR-014 as amended by US-010).</summary>
public interface IOverlayPublisher
{
    /// <summary>
    /// Sends the model to the newest verified tray connection of the session (fire-and-forget, not awaited). Never throws.
    /// Returns <see cref="KidMessageStates.ShownInOverlay"/> or <see cref="KidMessageStates.NotConnected"/>.
    /// </summary>
    string Publish(int sessionId, RemainingTimeDto model);
}
