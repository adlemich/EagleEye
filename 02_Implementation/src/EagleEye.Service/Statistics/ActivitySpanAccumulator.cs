namespace EagleEye.Service.Statistics;

/// <summary>
/// Builds the activity spans of ADR-021 §2 from the tracker's credited parts: per account and program path one open span; a
/// part that starts at most <see cref="JoinTolerance"/> away from the open span's end, on the same local day, extends it;
/// otherwise a new span starts (new day, gap, inactive time, clock jump). Not thread-safe (used by the tracker only).
/// </summary>
public sealed class ActivitySpanAccumulator
{
    /// <summary>The largest distance between a span's end and the next credited part that still extends the span.</summary>
    public static readonly TimeSpan JoinTolerance = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, SpanUpdate> _open = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Key, DateTimeOffset Start), SpanUpdate> _changed = [];

    /// <summary>Adds a credited part <c>[start, end]</c> of an app on a local day.</summary>
    public void Add(string sid, string path, DateOnly day, DateTimeOffset start, DateTimeOffset end)
    {
        var key = $"{sid.ToUpperInvariant()}|{path.ToUpperInvariant()}";
        var span = _open.TryGetValue(key, out var open) && open.Day == day && (start - open.EndUtc).Duration() <= JoinTolerance
            ? open with { EndUtc = end > open.EndUtc ? end : open.EndUtc }
            : new SpanUpdate(sid, path, day, start, end);
        _open[key] = span;
        _changed[(key, span.StartUtc)] = span;
    }

    /// <summary>Returns every span created or extended since the last call; forgets open spans of days before <paramref name="oldestKeptDay"/>.</summary>
    public IReadOnlyList<SpanUpdate> Take(DateOnly oldestKeptDay)
    {
        var updates = _changed.Values.ToList();
        _changed.Clear();
        foreach (var (key, span) in _open.ToList())
        {
            if (span.Day < oldestKeptDay)
            {
                _open.Remove(key);
            }
        }

        return updates;
    }
}
