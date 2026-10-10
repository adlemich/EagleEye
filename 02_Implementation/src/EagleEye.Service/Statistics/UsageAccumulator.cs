using System.Globalization;

namespace EagleEye.Service.Statistics;

/// <summary>
/// Exact credited time per account, app and local day, emitted as whole seconds (ADR-012 §4): the remainder
/// below one second is carried to the next output, so no time is lost systematically. Account SIDs and program
/// paths are compared ignoring case.
/// </summary>
public sealed class UsageAccumulator
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Adds credited time.</summary>
    public void Add(string sid, string path, DateOnly day, TimeSpan length)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{sid.ToUpperInvariant()}|{path.ToUpperInvariant()}|{day.DayNumber}");
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry(sid, path, day);
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
                credits.Add(new UsageCredit(entry.Sid, entry.Path, entry.Day, whole - entry.Emitted));
                entry.Emitted = whole;
            }

            if (entry.Day < oldestKeptDay)
            {
                _entries.Remove(key);
            }
        }

        return credits;
    }

    private sealed class Entry(string sid, string path, DateOnly day)
    {
        public string Sid { get; } = sid;

        public string Path { get; } = path;

        public DateOnly Day { get; } = day;

        public TimeSpan Exact { get; set; }

        public long Emitted { get; set; }
    }
}
