namespace EagleEye.Service.Statistics;

/// <summary>
/// Limits the instance start/end log entries per account and app to <see cref="MaxEntriesPerHour"/> per hour, so
/// that opening and closing a window in a loop cannot push older entries out of the log rotation (ADR-011 T-12).
/// Suppressed entries are counted and reported once when their hour is over (<see cref="TakeSummaries"/>).
/// </summary>
public sealed class InstanceLogLimiter(TimeProvider timeProvider)
{
    /// <summary>Start and end entries allowed per account and app within one hour.</summary>
    public const int MaxEntriesPerHour = 30;

    /// <summary>Length of a limiting window.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly Dictionary<(string Sid, string Path), Bucket> _buckets = [];

    /// <summary>Whether the next entry for the app may be written; counts it either way.</summary>
    public bool TryAcquire(string accountSid, string programPath, string userName, string displayName)
    {
        var key = (accountSid.ToUpperInvariant(), programPath.ToUpperInvariant());
        var now = timeProvider.GetTimestamp();
        if (!_buckets.TryGetValue(key, out var bucket) || timeProvider.GetElapsedTime(bucket.Start, now) >= Window)
        {
            if (bucket is { Suppressed: > 0 })
            {
                _pending.Add(bucket.ToSummary());
            }

            bucket = new Bucket(now, userName, displayName, programPath);
            _buckets[key] = bucket;
        }

        if (bucket.Count < MaxEntriesPerHour)
        {
            bucket.Count++;
            return true;
        }

        bucket.Suppressed++;
        return false;
    }

    /// <summary>Summaries of windows that are over and had suppressed entries; each is returned once.</summary>
    public IReadOnlyList<SuppressedEntries> TakeSummaries()
    {
        var now = timeProvider.GetTimestamp();
        foreach (var (key, bucket) in _buckets.ToList())
        {
            if (timeProvider.GetElapsedTime(bucket.Start, now) >= Window)
            {
                _buckets.Remove(key);
                if (bucket.Suppressed > 0)
                {
                    _pending.Add(bucket.ToSummary());
                }
            }
        }

        var result = _pending.ToList();
        _pending.Clear();
        return result;
    }

    private readonly List<SuppressedEntries> _pending = [];

    private sealed class Bucket(long start, string userName, string displayName, string programPath)
    {
        public long Start { get; } = start;

        public int Count { get; set; }

        public int Suppressed { get; set; }

        public SuppressedEntries ToSummary() => new(userName, displayName, programPath, Suppressed);
    }
}

/// <summary>Start/end entries of one app that were not logged within one hour.</summary>
public sealed record SuppressedEntries(string UserName, string DisplayName, string ProgramPath, int Count);
