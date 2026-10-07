namespace EagleEye.Service.Data;

/// <summary>Data access for the <c>AccountSelections</c> table (US-003, FR-SVC-072 to FR-SVC-074).</summary>
public interface IAccountSelectionRepository
{
    /// <summary>Returns all stored selections by SID (comparison ignores case).</summary>
    Task<IReadOnlyDictionary<string, bool>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Stores the selection of an account (insert or update).</summary>
    /// <param name="sid">The account's SID.</param>
    /// <param name="userName">The current logon name, kept for diagnosis only.</param>
    /// <param name="isUnderParentalControl">The selection.</param>
    /// <param name="changedAtUtc">Time of the change.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync(string sid, string userName, bool isUnderParentalControl, DateTimeOffset changedAtUtc, CancellationToken ct = default);

    /// <summary>
    /// Deletes the rows of all SIDs that are not in <paramref name="existingSids"/> (accounts deleted
    /// from the PC, AC-22) and returns the deleted SIDs.
    /// </summary>
    Task<IReadOnlyList<string>> DeleteMissingAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
