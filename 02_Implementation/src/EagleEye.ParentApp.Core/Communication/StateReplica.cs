namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// The client-side replica of one state area (ADR-010 §4): a snapshot is applied only if its revision
/// is higher than the last applied one, so query results and broadcasts that overtake each other never
/// show stale state. Revisions are only comparable within one connection: call <see cref="Reset"/> on
/// every new connection. Thread-safe. Reused by every state area.
/// </summary>
/// <typeparam name="T">The snapshot DTO of the area.</typeparam>
public sealed class StateReplica<T>
    where T : class
{
    private readonly Func<T, long> _revisionOf;
    private readonly Lock _lock = new();
    private T? _current;
    private long _revision;

    /// <summary>Creates an empty replica.</summary>
    /// <param name="revisionOf">Reads the revision of a snapshot.</param>
    public StateReplica(Func<T, long> revisionOf)
    {
        _revisionOf = revisionOf ?? throw new ArgumentNullException(nameof(revisionOf));
    }

    /// <summary>The last applied snapshot, or <c>null</c> after a reset.</summary>
    public T? Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>The revision of the last applied snapshot (0 after a reset).</summary>
    public long Revision
    {
        get
        {
            lock (_lock)
            {
                return _revision;
            }
        }
    }

    /// <summary>Applies the snapshot if its revision is higher; returns whether it was applied.</summary>
    public bool TryApply(T snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var revision = _revisionOf(snapshot);
        lock (_lock)
        {
            if (revision <= _revision)
            {
                return false;
            }

            _current = snapshot;
            _revision = revision;
            return true;
        }
    }

    /// <summary>Forgets the snapshot and the revision (new connection).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _current = null;
            _revision = 0;
        }
    }
}
