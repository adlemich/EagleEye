namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Decides when the agent writes a report (ADR-011 §3): at once when the set of apps changed, otherwise
/// as a heartbeat every <see cref="HeartbeatInterval"/>. Numbers the reports.
/// </summary>
public sealed class AgentReportPublisher(TimeProvider timeProvider)
{
    /// <summary>A report is written at least this often.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private IReadOnlyList<AgentApp>? _last;
    private bool _lastTruncated;
    private long _lastWritten;
    private long _seq;

    /// <summary>Returns the report line to write for this scan, or <c>null</c> if nothing is due.</summary>
    public string? Next(IReadOnlyList<AgentApp> apps, bool truncated)
    {
        ArgumentNullException.ThrowIfNull(apps);
        var now = timeProvider.GetTimestamp();
        var changed = _last is null || truncated != _lastTruncated || !_last.SequenceEqual(apps);
        if (!changed && timeProvider.GetElapsedTime(_lastWritten, now) < HeartbeatInterval)
        {
            return null;
        }

        _last = apps;
        _lastTruncated = truncated;
        _lastWritten = now;
        return AgentProtocol.Serialize(new AgentReport(++_seq, truncated, apps));
    }
}
