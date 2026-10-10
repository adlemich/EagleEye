using EagleEye.Service.Data;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Retention of the enforcement records (AC-36, AC-37, FR-SVC-043, FR-SVC-047): the history of blocked starts and the
/// time-change findings are kept for <see cref="RetentionDays"/> days and deleted with their account. At service start,
/// records left open by a crash get the outcome "unknown (service stopped)".
/// </summary>
public sealed class EnforcementHistory(
    IBlockedStartRepository blockedStarts,
    ITimeChangeFindingRepository findings,
    TimeProvider timeProvider,
    ILogger<EnforcementHistory> logger) : IAccountDataPurger
{
    /// <summary>Days the records are kept.</summary>
    public const int RetentionDays = 90;

    /// <summary>Completes dangling records and purges old ones (service start).</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dangling = await blockedStarts.CompleteDanglingAsync(BlockedStartTexts.UnknownServiceStopped, timeProvider.GetUtcNow(), ct)
            .ConfigureAwait(false);
        if (dangling > 0)
        {
            logger.LogInformation("{Count} blocked start(s) left open by a service stop were completed as \"{Outcome}\".", dangling, BlockedStartTexts.UnknownServiceStopped);
        }

        await PurgeOldDataAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Deletes records older than 90 days (start and first tick after local midnight).</summary>
    public async Task PurgeOldDataAsync(CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-RetentionDays);
        var starts = await blockedStarts.PurgeOlderThanAsync(cutoff, ct).ConfigureAwait(false);
        var changes = await findings.PurgeOlderThanAsync(cutoff, ct).ConfigureAwait(false);
        if (starts + changes > 0)
        {
            logger.LogInformation("Purged enforcement records older than 90 days: {BlockedStarts} blocked starts, {Findings} time-change findings.", starts, changes);
        }
    }

    /// <inheritdoc />
    public async Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        var starts = await blockedStarts.PurgeAccountsNotInAsync(existingSids, ct).ConfigureAwait(false);
        var changes = await findings.PurgeAccountsNotInAsync(existingSids, ct).ConfigureAwait(false);
        if (starts + changes > 0)
        {
            logger.LogInformation("Purged enforcement records of deleted accounts: {BlockedStarts} blocked starts, {Findings} time-change findings.", starts, changes);
        }
    }
}
