namespace EagleEye.Service.UserAccounts;

/// <summary>
/// Immutable inventory: the standard accounts by SID (<see cref="AccountInventoryFilter.Standard"/>)
/// and the SIDs of <b>all</b> local accounts, standard and admin (needed for AC-21/AC-22: a stored
/// selection is kept while its account exists, also as an admin).
/// </summary>
public sealed record AccountInventory
{
    private AccountInventory(IReadOnlyDictionary<string, LocalAccountInfo> standardAccounts, IReadOnlySet<string> allSids)
    {
        StandardAccounts = standardAccounts;
        AllSids = allSids;
    }

    /// <summary>The standard accounts by SID (ignoring case).</summary>
    public IReadOnlyDictionary<string, LocalAccountInfo> StandardAccounts { get; }

    /// <summary>The SIDs of all local accounts (ignoring case).</summary>
    public IReadOnlySet<string> AllSids { get; }

    /// <summary>Builds the inventory from all local accounts.</summary>
    public static AccountInventory Create(IEnumerable<LocalAccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var all = accounts.ToList();
        return new AccountInventory(
            AccountInventoryFilter.Standard(all).ToDictionary(a => a.Sid, StringComparer.OrdinalIgnoreCase),
            all.Select(a => a.Sid).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Whether both inventories have the same standard accounts with the same fields.</summary>
    public bool HasSameStandardAccounts(AccountInventory other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StandardAccounts.Count == other.StandardAccounts.Count
            && StandardAccounts.All(pair => other.StandardAccounts.TryGetValue(pair.Key, out var account) && account == pair.Value);
    }

    /// <summary>Whether both inventories are equal: same standard accounts and same set of all SIDs.</summary>
    public bool HasSameContent(AccountInventory other)
    {
        return HasSameStandardAccounts(other) && AllSids.SetEquals(other.AllSids);
    }

    /// <summary>
    /// The user names of the standard accounts added, removed and changed (name, full name, disabled
    /// flag) compared with <paramref name="previous"/> (<c>null</c>: everything is added), for the log.
    /// </summary>
    public (IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed) Compare(AccountInventory? previous)
    {
        var before = previous?.StandardAccounts ?? new Dictionary<string, LocalAccountInfo>();
        var added = StandardAccounts.Values.Where(a => !before.ContainsKey(a.Sid)).Select(a => a.UserName);
        var removed = before.Values.Where(a => !StandardAccounts.ContainsKey(a.Sid)).Select(a => a.UserName);
        var changed = StandardAccounts.Values
            .Where(a => before.TryGetValue(a.Sid, out var old) && old != a)
            .Select(a => a.UserName);
        return (Sorted(added), Sorted(removed), Sorted(changed));
    }

    private static List<string> Sorted(IEnumerable<string> names) => names.Order(StringComparer.OrdinalIgnoreCase).ToList();
}
