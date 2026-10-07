namespace EagleEye.Service.Statistics;

/// <summary>
/// Exact credited time per account, app and local day, emitted as whole seconds (ADR-012 §4): the remainder
/// below one second is carried to the next output, so no time is lost systematically.
/// </summary>
public sealed class UsageAccumulator
{
    private readonly Dictionary<(string Sid, string Path, DateOnly Day), Entry> _entries = new(KeyComparer.Instance);

    /// <summary>Adds credited time.</summary>
    public void Add(string sid, string path, DateOnly day, TimeSpan length)
    {
        var key = (sid, path, day);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            _entries[key] = entry;
        }

        entry.Exact += length;
    }

    /// <summary>
    /// Returns the whole seconds not emitted yet, and forgets days before <paramref name="oldestKeptDay"/>
    /// once they are fully emitted.
    /// </summary>
    public IReadOnlyList<UsageCredit> Emit(DateOnly oldestKeptDay)
    {
        var credits = new List<UsageCredit>();
        foreach (var (key, entry) in _entries.ToList())
        {
            var whole = (long)Math.Floor(entry.Exact.TotalSeconds);
            if (whole > entry.Emitted)
            {
                credits.Add(new UsageCredit(key.Sid, key.Path, key.Day, whole - entry.Emitted));
                entry.Emitted = whole;
            }

            if (key.Day < oldestKeptDay)
            {
                _entries.Remove(key);
            }
        }

        return credits;
    }

    private sealed class Entry
    {
        public TimeSpan Exact { get; set; }

        public long Emitted { get; set; }
    }

    private sealed class KeyComparer : IEqualityComparer<(string Sid, string Path, DateOnly Day)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((string Sid, string Path, DateOnly Day) x, (string Sid, string Path, DateOnly Day) y)
        {
            return x.Day == y.Day
                && StringComparer.OrdinalIgnoreCase.Equals(x.Sid, y.Sid)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path);
        }

        public int GetHashCode((string Sid, string Path, DateOnly Day) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Sid),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path),
                obj.Day);
        }
    }
}
