using EagleEye.Shared.Constants;

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
}
