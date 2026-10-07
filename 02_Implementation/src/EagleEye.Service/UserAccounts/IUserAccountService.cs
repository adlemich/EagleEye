using EagleEye.Shared.Models;

namespace EagleEye.Service.UserAccounts;

/// <summary>
/// State owner of the state area "UserAccounts" (ADR-010 §2, §9): the inventory of standard accounts
/// and the parent's selection, with one revision and one lock.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Loads the selections and reads the first inventory (revision 1). A failed enumeration leaves
    /// the inventory unavailable; it is retried by <see cref="RefreshInventoryAsync"/>.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Returns the current snapshot.</summary>
    /// <exception cref="AccountInventoryUnavailableException">The inventory has not been read yet.</exception>
    Task<UserAccountListDto> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the selection, increments the revision, logs, broadcasts and returns the acknowledgement.
    /// </summary>
    /// <param name="requestId">The client's correlation id.</param>
    /// <param name="sid">The account's SID.</param>
    /// <param name="isUnderParentalControl">The new selection.</param>
    /// <param name="deviceName">The name of the paired device that sent the change (for the log).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UnknownAccountException">The SID is not a standard account of the inventory.</exception>
    /// <exception cref="AccountInventoryUnavailableException">The inventory has not been read yet.</exception>
    Task<StateWriteAckDto> SetParentalControlAsync(
        Guid requestId, string sid, bool isUnderParentalControl, string deviceName, CancellationToken ct = default);

    /// <summary>
    /// Reads the accounts again; if anything changed, forgets selections of deleted accounts,
    /// increments the revision, logs and broadcasts (<c>LastChangeRequestId = null</c>).
    /// </summary>
    Task RefreshInventoryAsync(CancellationToken ct = default);
}
