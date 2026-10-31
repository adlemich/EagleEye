using EagleEye.Service.Data;

namespace EagleEye.Service.Groups;

/// <summary>
/// The apps of the App Groups page (US-008 AC-3, TI-5): all app records (ignore list excluded by the caller) and the
/// controlled accounts; listed is every program path with a record in at least one controlled account, once, with the display
/// name of its most recently seen record. Not thread-safe: used under the lock of <see cref="AppGroupService"/>.
/// </summary>
public sealed class AppGroupCatalog
{
    private readonly Dictionary<string, StoredCatalogApp> _records = new(StringComparer.Ordinal);
    private HashSet<string> _controlled = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, StoredCatalogApp> _listed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The listed apps by program path (case-insensitive).</summary>
    public IReadOnlyDictionary<string, StoredCatalogApp> Listed => _listed;

    /// <summary>Adds or updates a record; returns whether the listed apps or their names changed.</summary>
    public bool See(StoredCatalogApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _records[$"{app.AccountSid.ToUpperInvariant()}|{app.ProgramPath.ToUpperInvariant()}"] = app;
        return Refresh();
    }

    /// <summary>Sets the controlled accounts; returns whether the listed apps changed.</summary>
    public bool SetControlled(IReadOnlyCollection<string> accountSids)
    {
        ArgumentNullException.ThrowIfNull(accountSids);
        var controlled = new HashSet<string>(accountSids, StringComparer.OrdinalIgnoreCase);
        if (controlled.SetEquals(_controlled))
        {
            return false;
        }

        _controlled = controlled;
        return Refresh();
    }

    /// <summary>Forgets the records of accounts not in <paramref name="existingSids"/>; returns whether the listed apps changed.</summary>
    public bool RemoveAccountsNotIn(IReadOnlyCollection<string> existingSids)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        var existing = new HashSet<string>(existingSids, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, app) in _records.ToList())
        {
            if (!existing.Contains(app.AccountSid))
            {
                _records.Remove(key);
            }
        }

        return Refresh();
    }

    private bool Refresh()
    {
        var listed = _records.Values
            .Where(r => _controlled.Contains(r.AccountSid))
            .GroupBy(r => r.ProgramPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.MaxBy(r => r.LastSeenUtc)!, StringComparer.OrdinalIgnoreCase); // A group is never empty.
        var changed = listed.Count != _listed.Count
            || listed.Any(l => !_listed.TryGetValue(l.Key, out var old) || old.DisplayName != l.Value.DisplayName || old.ProgramPath != l.Value.ProgramPath);
        _listed = listed;
        return changed;
    }
}
