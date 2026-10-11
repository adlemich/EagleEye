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

    /// <summary>
    /// Returns the recorded usage of one account (state areas "UsageDay:{sid}:{day}", ADR-012 §6):
    /// today (always present, possibly without apps) and every other day of the last 90 days with
    /// usage, newest first. Called after every (re)connect and when the parent selects another account.
    /// </summary>
    /// <param name="accountSid">SID of a standard account of the inventory (US-003), controlled or not.</param>
    Task<AccountUsageDto> GetAccountUsage(string accountSid);

    /// <summary>
    /// Returns the break times and the display text of one account (state area "AccountRules:{sid}", ADR-010).
    /// Called after every (re)connect and when the parent selects another account on the Rules page.
    /// </summary>
    /// <param name="accountSid">SID of a standard account of the inventory (US-003), controlled or not.</param>
    Task<AccountRulesDto> GetAccountRules(string accountSid);

    /// <summary>
    /// Adds an entry with the defaults of US-005 AC-7 (off, 20:00–23:59, all days) at the end.
    /// Throws <c>HubException</c> when the account already has <see cref="BreakTimeRules.MaxEntriesPerAccount"/> entries.
    /// </summary>
    /// <param name="requestId">Client-generated correlation id, echoed in <see cref="AccountRulesDto.LastChangeRequestId"/>.</param>
    /// <param name="accountSid">The account.</param>
    Task<StateWriteAckDto> AddBreakTimeEntry(Guid requestId, string accountSid);

    /// <summary>Deletes an entry. Throws <c>HubException</c> ("The entry no longer exists.") if it is gone.</summary>
    /// <param name="requestId">Client-generated correlation id.</param>
    /// <param name="accountSid">The account.</param>
    /// <param name="entryId">The entry.</param>
    Task<StateWriteAckDto> DeleteBreakTimeEntry(Guid requestId, string accountSid, long entryId);

    /// <summary>Switches an entry on or off (AC-14).</summary>
    /// <param name="requestId">Client-generated correlation id.</param>
    /// <param name="accountSid">The account.</param>
    /// <param name="entryId">The entry.</param>
    /// <param name="isActive">The new state of the switch.</param>
    Task<StateWriteAckDto> SetBreakTimeEntryActive(Guid requestId, string accountSid, long entryId, bool isActive);

    /// <summary>
    /// Sets the start or the end time in minutes after midnight (0 = 00:00, 1439 = 23:59 = "until midnight").
    /// Validated against the stored other boundary: the end must be later than the start (AC-10).
    /// </summary>
    /// <param name="requestId">Client-generated correlation id.</param>
    /// <param name="accountSid">The account.</param>
    /// <param name="entryId">The entry.</param>
    /// <param name="boundary">Which time is changed.</param>
    /// <param name="minute">The new time, 0 … 1439.</param>
    Task<StateWriteAckDto> SetBreakTimeEntryTime(Guid requestId, string accountSid, long entryId, BreakTimeBoundary boundary, int minute);

    /// <summary>Ticks or unticks one weekday. At least one day must stay ticked (AC-11).</summary>
    /// <param name="requestId">Client-generated correlation id.</param>
    /// <param name="accountSid">The account.</param>
    /// <param name="entryId">The entry.</param>
    /// <param name="day">The weekday.</param>
    /// <param name="isSelected">Whether the day is ticked.</param>
    Task<StateWriteAckDto> SetBreakTimeEntryDay(Guid requestId, string accountSid, long entryId, DayOfWeek day, bool isSelected);

    /// <summary>
    /// Sets the display text (FR-APP-052). Line breaks are normalized to "\n". Empty or white space only →
    /// the default text is restored (OQ-8). At most <see cref="BreakTimeRules.MaxDisplayTextLength"/> UTF-16 code units.
    /// </summary>
    /// <param name="requestId">Client-generated correlation id.</param>
    /// <param name="accountSid">The account.</param>
    /// <param name="text">The new text.</param>
    Task<StateWriteAckDto> SetDisplayText(Guid requestId, string accountSid, string text);
}
