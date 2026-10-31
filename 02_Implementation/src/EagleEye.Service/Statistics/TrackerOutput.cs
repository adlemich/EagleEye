namespace EagleEye.Service.Statistics;

/// <summary>An instance event of the tracker (history, AC-10).</summary>
/// <param name="InstanceKey">The tracker's key of the instance (mapped to the database id by the usage service).</param>
/// <param name="AccountSid">The account.</param>
/// <param name="ProgramPath">The app's program path.</param>
/// <param name="ProcessName">The process name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="StartedUtc">Start of the instance.</param>
public abstract record InstanceEvent(
    long InstanceKey, string AccountSid, string ProgramPath, string ProcessName, string DisplayName, DateTimeOffset StartedUtc);

/// <summary>An app instance started.</summary>
public sealed record InstanceStarted(
    long InstanceKey, string AccountSid, string ProgramPath, string ProcessName, string DisplayName, DateTimeOffset StartedUtc)
    : InstanceEvent(InstanceKey, AccountSid, ProgramPath, ProcessName, DisplayName, StartedUtc);

/// <summary>An app instance ended.</summary>
/// <param name="EndedUtc">End of the instance.</param>
/// <param name="Reason">One of <see cref="EndReasons"/>.</param>
public sealed record InstanceEnded(
    long InstanceKey, string AccountSid, string ProgramPath, string ProcessName, string DisplayName, DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc, string Reason)
    : InstanceEvent(InstanceKey, AccountSid, ProgramPath, ProcessName, DisplayName, StartedUtc);

/// <summary>Whole seconds credited to an app on a local day since the last output.</summary>
public sealed record UsageCredit(string AccountSid, string ProgramPath, DateOnly Day, long Seconds);

/// <summary>An open instance was seen at this time (stored as "last seen", the end time after a crash).</summary>
public sealed record InstanceSeen(long InstanceKey, DateTimeOffset LastSeenUtc);

/// <summary>A new or extended activity span of an app (ADR-021 §2): an interval on one local day during which the app was credited.</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="ProgramPath">The app's program path.</param>
/// <param name="Day">The local day.</param>
/// <param name="StartUtc">Start of the span (its identity per app).</param>
/// <param name="EndUtc">The current end of the span.</param>
public sealed record SpanUpdate(string AccountSid, string ProgramPath, DateOnly Day, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>What the tracker produced since its last output.</summary>
public sealed record TrackerOutput(
    IReadOnlyList<InstanceEvent> Instances, IReadOnlyList<UsageCredit> Credits, IReadOnlyList<InstanceSeen> Seen)
{
    /// <summary>The activity spans created or extended since the last tick output (ticks only).</summary>
    public IReadOnlyList<SpanUpdate> Spans { get; init; } = [];

    /// <summary>No output.</summary>
    public static readonly TrackerOutput Empty = new([], [], []);
}
