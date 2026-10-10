namespace EagleEye.Service.Data;

/// <summary>Data access for <c>AppRecords</c>, <c>AppInstances</c> and <c>DailyUsage</c> (US-004, ADR-012 §5).</summary>
public interface IUsageRepository
{
    /// <summary>Returns the app record of the account and program path (case-insensitive), creating it if needed;
    /// updates process name, display name and last seen time.</summary>
    Task<AppRecordResult> GetOrCreateAppAsync(
        string accountSid, string programPath, string processName, string displayName, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Stores the start of an instance and returns its id.</summary>
    Task<long> StartInstanceAsync(long appId, DateTimeOffset startedUtc, CancellationToken ct = default);

    /// <summary>Stores the end of an instance.</summary>
    Task EndInstanceAsync(long instanceId, DateTimeOffset endedUtc, string reason, CancellationToken ct = default);

    /// <summary>Returns all instances without end.</summary>
    Task<IReadOnlyList<OpenInstanceRecord>> GetOpenInstancesAsync(CancellationToken ct = default);

    /// <summary>Adds the seconds and updates "last seen" of open instances, in one transaction.</summary>
    Task ApplyAsync(IReadOnlyCollection<UsageIncrement> increments, IReadOnlyCollection<InstanceSeenRecord> seen, CancellationToken ct = default);

    /// <summary>Returns the usage of the account between two local days (inclusive).</summary>
    Task<IReadOnlyList<DayAppUsage>> GetUsageAsync(string accountSid, DateOnly fromDay, DateOnly toDay, CancellationToken ct = default);

    /// <summary>Deletes daily usage before <paramref name="cutoffDay"/> and ended instances started before
    /// <paramref name="cutoffUtc"/>; returns the numbers of deleted rows.</summary>
    Task<(int DayRows, int Instances)> PurgeOlderThanAsync(DateOnly cutoffDay, DateTimeOffset cutoffUtc, CancellationToken ct = default);

    /// <summary>Deletes all data of accounts that are not in <paramref name="existingSids"/>.</summary>
    Task<IReadOnlyList<AccountPurge>> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
