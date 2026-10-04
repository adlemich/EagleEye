using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Shared.Contracts;

/// <summary>
/// Server-side hub for parent apps (route <see cref="HubRoutes.Parent"/>, HTTPS only, ADR-008).
/// Every method requires a paired connection unless it is marked <see cref="AllowUnpairedAttribute"/>.
/// </summary>
public interface IParentHub
{
    /// <summary>Whether this connection is authenticated as a paired device. Called after every (re)connect.</summary>
    [AllowUnpaired]
    Task<PairingStatusDto> GetPairingStatus();

    /// <summary>
    /// Generates a new pairing code bound to this connection and shows it on the service PC
    /// (tray clients, or the Event Log if none is connected). Replaces any pending code.
    /// Callable by unpaired connections only.
    /// </summary>
    [AllowUnpaired]
    Task StartPairing();

    /// <summary>
    /// Submits the code and the device name. Any failure invalidates the pending code.
    /// Callable by unpaired connections only.
    /// </summary>
    /// <param name="code">The 6-digit pairing code shown on the service PC.</param>
    /// <param name="deviceName">The name of the parent device, see <see cref="PairingRules.NormalizeDeviceName"/>.</param>
    [AllowUnpaired]
    Task<PairingResultDto> SubmitPairingCode(string code, string deviceName);

    /// <summary>De-registers a paired device (FR-SVC-097). Other connections of that device are closed.</summary>
    /// <param name="deviceId">The device ID returned by a successful pairing.</param>
    Task RemovePairedDevice(string deviceId);
}
