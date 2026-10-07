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

    /// <summary>
    /// Returns the current inventory of standard accounts of the service PC with their selection
    /// (state area "UserAccounts", ADR-010). Called by the app after every (re)connect.
    /// </summary>
    Task<UserAccountListDto> GetUserAccounts();

    /// <summary>
    /// Places an account under parental control or removes it (FR-SVC-072, FR-APP-022). The service
    /// stores the value, broadcasts the new snapshot to all paired apps including the caller
    /// (<see cref="IParentClientCallback.OnUserAccountsChanged"/>), and then returns the revision.
    /// Last write wins. Throws <c>HubException</c> if the account is not in the inventory or the
    /// value cannot be stored.
    /// </summary>
    /// <param name="requestId">Client-generated correlation id, echoed in <see cref="UserAccountListDto.LastChangeRequestId"/>.</param>
    /// <param name="accountSid">The account's SID as delivered in <see cref="UserAccountDto.Sid"/>.</param>
    /// <param name="isUnderParentalControl">The new state.</param>
    Task<StateWriteAckDto> SetParentalControl(Guid requestId, string accountSid, bool isUnderParentalControl);
}
