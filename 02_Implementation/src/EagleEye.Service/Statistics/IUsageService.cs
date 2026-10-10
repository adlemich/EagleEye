using EagleEye.Shared.Models;

namespace EagleEye.Service.Statistics;

/// <summary>
/// State owner of the usage areas "UsageDay:{sid}:{day}" (ADR-010 §9, ADR-012 §5 and §6): persists what the
/// tracker produces, writes the history log entries, broadcasts changed days and answers queries.
/// </summary>
public interface IUsageService
{
    /// <summary>At start: ends instances left open by a crash at their last recorded time, purges old data.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the tracker's output: app records and instance starts/ends at once; on a tick also the credited
    /// seconds and "last seen" times (one transaction) followed by one broadcast per changed account and day.
    /// </summary>
    /// <param name="output">The tracker's output.</param>
    /// <param name="userNames">User names by SID, for the log.</param>
    /// <param name="isTick">Whether this is the 5 s tick (persist and broadcast usage).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyAsync(TrackerOutput output, IReadOnlyDictionary<string, string> userNames, bool isTick, CancellationToken ct = default);

    /// <summary>Today plus every day of the last 90 with usage, newest first (AC-18, AC-19).</summary>
    /// <exception cref="UserAccounts.UnknownAccountException">The SID is not a standard account of the inventory.</exception>
    Task<AccountUsageDto> GetAccountUsageAsync(string accountSid, CancellationToken ct = default);

    /// <summary>Deletes daily usage and history older than 90 days (AC-11, FR-SVC-043).</summary>
    Task PurgeOldDataAsync(CancellationToken ct = default);

    /// <summary>Broadcasts today's (new, usually empty) snapshot of each account (local midnight, ADR-012 §6).</summary>
    Task PublishTodayAsync(IReadOnlyCollection<string> accountSids, CancellationToken ct = default);
}

/// <summary>Deletes all recorded data of accounts that no longer exist on the PC (AC-9, FR-SVC-047).</summary>
public interface IAccountDataPurger
{
    /// <summary>Deletes app records, history and usage of every SID that is not in <paramref name="existingSids"/>.</summary>
    Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}

/// <summary>Sends usage snapshots to all paired parent apps.</summary>
public interface IUsageBroadcaster
{
    /// <summary>Broadcasts the day snapshot to the group <c>Parents</c>.</summary>
    Task BroadcastAsync(DayUsageDto snapshot);
}
