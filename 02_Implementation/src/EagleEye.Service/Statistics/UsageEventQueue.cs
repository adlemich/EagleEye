using System.Threading.Channels;

namespace EagleEye.Service.Statistics;

/// <summary>
/// The single input queue of the accounting loop (one reader). Agent reports are kept <b>latest-only per
/// session</b>: a newer report replaces an unprocessed older one, so a flood of reports cannot grow memory
/// (ADR-011 §7 item 11, T-8). Other events (ticks, session and power notifications) are few and kept in order.
/// </summary>
public sealed class UsageEventQueue
{
    private readonly Channel<QueueItem> _channel = Channel.CreateUnbounded<QueueItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<int, AppsObserved> _latestReports = [];
    private readonly Lock _lock = new();

    /// <summary>Number of agent reports waiting (at most one per session).</summary>
    public int PendingReports
    {
        get
        {
            lock (_lock)
            {
                return _latestReports.Count;
            }
        }
    }

    /// <summary>Queues an event; returns <c>false</c> after <see cref="Complete"/>.</summary>
    public bool Enqueue(UsageEvent usageEvent)
    {
        ArgumentNullException.ThrowIfNull(usageEvent);
        if (usageEvent is not AppsObserved report)
        {
            return _channel.Writer.TryWrite(new QueueItem(usageEvent, null));
        }

        lock (_lock)
        {
            var isNew = !_latestReports.ContainsKey(report.SessionId);
            _latestReports[report.SessionId] = report;
            return !isNew || _channel.Writer.TryWrite(new QueueItem(null, report.SessionId));
        }
    }

    /// <summary>Waits for the next event.</summary>
    /// <exception cref="ChannelClosedException">The queue was completed and is empty.</exception>
    public async ValueTask<UsageEvent> DequeueAsync(CancellationToken ct)
    {
        var item = await _channel.Reader.ReadAsync(ct).ConfigureAwait(false);
        if (item.Event is { } usageEvent)
        {
            return usageEvent;
        }

        lock (_lock)
        {
            // Each marker has exactly one pending report: markers are written only for new entries, and only
            // the reader removes entries.
            _latestReports.Remove(item.ReportSessionId!.Value, out var report);
            return report!;
        }
    }

    /// <summary>No more events are accepted (service stopping).</summary>
    public void Complete() => _channel.Writer.TryComplete();

    private sealed record QueueItem(UsageEvent? Event, int? ReportSessionId);
}
