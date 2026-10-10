namespace EagleEye.Service.Data;

/// <summary>Data access for the history of blocked starts <c>BlockedStarts</c> (ADR-013 §7, AC-35, AC-36).</summary>
public interface IBlockedStartRepository
{
    /// <summary>Stores a new record with an open outcome and returns its id.</summary>
    Task<long> InsertAsync(BlockedStartRecord record, CancellationToken ct = default);

    /// <summary>Stores the result of a blocked start.</summary>
    /// <param name="blockedStartId">The record.</param>
    /// <param name="outcome">The outcome text.</param>
    /// <param name="secondsUntilGone">Seconds from detection until the app was gone; <c>null</c> if unknown.</param>
    /// <param name="messageState">Whether the message was shown.</param>
    /// <param name="completedUtc">When the sequence ended.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CompleteAsync(long blockedStartId, string outcome, double? secondsUntilGone, string messageState, DateTimeOffset completedUtc, CancellationToken ct = default);

    /// <summary>Sets <paramref name="outcome"/> on every record whose outcome is still open; returns their number.</summary>
    Task<int> CompleteDanglingAsync(string outcome, DateTimeOffset completedUtc, CancellationToken ct = default);

    /// <summary>Deletes records detected before <paramref name="cutoffUtc"/>; returns their number.</summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default);

    /// <summary>Deletes the records of all accounts that are not in <paramref name="existingSids"/>; returns their number.</summary>
    Task<int> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
