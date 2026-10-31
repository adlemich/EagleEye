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

/// <summary>A new or extended activity span of an app record (ADR-021 §2); identified by app record and start.</summary>
/// <param name="AppId">The app record.</param>
/// <param name="Day">The local day of the span (spans are split at local midnight).</param>
/// <param name="StartUtc">Start of the span.</param>
/// <param name="EndUtc">The (new) end of the span.</param>
public sealed record ActivitySpanWrite(long AppId, DateOnly Day, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>A stored activity span of an account's app on a local day.</summary>
/// <param name="Day">The local day.</param>
/// <param name="ProgramPath">The app's program path.</param>
/// <param name="StartUtc">Start of the span.</param>
/// <param name="EndUtc">End of the span.</param>
public sealed record StoredSpan(DateOnly Day, string ProgramPath, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
