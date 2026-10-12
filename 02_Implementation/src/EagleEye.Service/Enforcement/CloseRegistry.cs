namespace EagleEye.Service.Enforcement;

/// <summary>A running close sequence of an app in a session, as the gate and the engine see it.</summary>
/// <param name="Handle">The sequence.</param>
/// <param name="RemovesFromReport">Whether the app's processes are removed from the report (blocked starts only).</param>
/// <param name="Known">The processes already part of the sequence (PID + creation time).</param>
public sealed record CloseEntry(ICloseSequenceHandle Handle, bool RemovesFromReport, HashSet<(int Pid, long Created)> Known);

/// <summary>
/// The running close sequences per (session, program path) (US-006 Decision 8). The start gate and the time-limit engine
/// both use it, so an app is never closed by two sequences. Completed sequences are dropped on lookup. Thread-safe.
/// </summary>
public sealed class CloseRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<(int SessionId, string Path), CloseEntry> _entries = new(new KeyComparer());

    /// <summary>The running sequence of the app in the session, or <c>null</c> (also when it has completed).</summary>
    public CloseEntry? Find(int sessionId, string programPath)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        lock (_lock)
        {
            if (!_entries.TryGetValue((sessionId, programPath), out var entry))
            {
                return null;
            }

            if (!entry.Handle.IsCompleted)
            {
                return entry;
            }

            _entries.Remove((sessionId, programPath));
            return null;
        }
    }

    /// <summary>Whether a sequence for the app in the session is running.</summary>
    public bool IsClosing(int sessionId, string programPath) => Find(sessionId, programPath) is not null;

    /// <summary>Registers a started sequence (replaces a completed one).</summary>
    public void Add(int sessionId, string programPath, CloseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            _entries[(sessionId, programPath)] = entry;
        }
    }

    /// <summary>The session ended: forget its entries (the sequences themselves go on).</summary>
    public void RemoveSession(int sessionId)
    {
        lock (_lock)
        {
            foreach (var key in _entries.Keys.Where(k => k.SessionId == sessionId).ToList())
            {
                _entries.Remove(key);
            }
        }
    }

    private sealed class KeyComparer : IEqualityComparer<(int SessionId, string Path)>
    {
        public bool Equals((int SessionId, string Path) x, (int SessionId, string Path) y) =>
            x.SessionId == y.SessionId && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path);

        public int GetHashCode((int SessionId, string Path) obj) =>
            HashCode.Combine(obj.SessionId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
    }
}
