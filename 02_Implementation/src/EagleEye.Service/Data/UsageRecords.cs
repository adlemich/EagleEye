namespace EagleEye.Service.Data;

/// <summary>Result of looking up or creating an app record.</summary>
/// <param name="AppId">The app record.</param>
/// <param name="Created">The record was created by this call (first start of the app by the account, AC-7).</param>
public sealed record AppRecordResult(long AppId, bool Created);

/// <summary>An instance without end (left open by a crash, closed at the next start).</summary>
public sealed record OpenInstanceRecord(
    long InstanceId, string AccountSid, string ProgramPath, string ProcessName, string DisplayName,
    DateTimeOffset StartedUtc, DateTimeOffset LastSeenUtc);

/// <summary>Seconds to add to an app's usage on a day (may be 0, to create the row).</summary>
public sealed record UsageIncrement(long AppId, DateOnly Day, long Seconds);

/// <summary>New "last seen" time of an open instance.</summary>
public sealed record InstanceSeenRecord(long InstanceId, DateTimeOffset LastSeenUtc);

/// <summary>Usage of one app on one day.</summary>
public sealed record DayAppUsage(DateOnly Day, long AppId, string DisplayName, long Seconds);

/// <summary>What was deleted for an account that no longer exists (FR-SVC-047).</summary>
public sealed record AccountPurge(string AccountSid, int Apps, int Instances, int DayRows);
