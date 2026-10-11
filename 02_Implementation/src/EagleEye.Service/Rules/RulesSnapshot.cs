using System.Collections.Immutable;
using EagleEye.Service.Data;

namespace EagleEye.Service.Rules;

/// <summary>
/// Immutable copy of the rules of all accounts (ADR-013 §1). Replaced as a whole on every change and read
/// lock-free by the break-time gate. Keyed by SID (case-insensitive).
/// </summary>
public sealed class RulesSnapshot
{
    private readonly ImmutableDictionary<string, AccountRules> _accounts;

    private RulesSnapshot(ImmutableDictionary<string, AccountRules> accounts)
    {
        _accounts = accounts;
    }

    /// <summary>No rules at all.</summary>
    public static RulesSnapshot Empty { get; } = new(ImmutableDictionary.Create<string, AccountRules>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The SIDs that have stored rules.</summary>
    public IEnumerable<string> AccountSids => _accounts.Keys;

    /// <summary>Builds the snapshot from the stored rows.</summary>
    public static RulesSnapshot From(StoredRules stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var snapshot = Empty;
        foreach (var group in stored.Entries.GroupBy(e => e.AccountSid, StringComparer.OrdinalIgnoreCase))
        {
            var rules = snapshot.For(group.Key);
            snapshot = snapshot.With(group.Key, rules with { Entries = [.. group.Select(e => e.Entry)] });
        }

        foreach (var text in stored.Texts)
        {
            snapshot = snapshot.With(text.AccountSid, snapshot.For(text.AccountSid) with { CustomText = text.Text });
        }

        return snapshot;
    }

    /// <summary>The rules of the account; <see cref="AccountRules.Empty"/> if nothing is stored.</summary>
    public AccountRules For(string accountSid)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        return _accounts.TryGetValue(accountSid, out var rules) ? rules : AccountRules.Empty;
    }

    /// <summary>A copy with the account's rules replaced (removed if empty).</summary>
    public RulesSnapshot With(string accountSid, AccountRules rules)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(rules);
        return new RulesSnapshot(rules.IsEmpty ? _accounts.Remove(accountSid) : _accounts.SetItem(accountSid, rules));
    }

    /// <summary>A copy without the accounts.</summary>
    public RulesSnapshot Without(IEnumerable<string> accountSids)
    {
        ArgumentNullException.ThrowIfNull(accountSids);
        return new RulesSnapshot(_accounts.RemoveRange(accountSids));
    }
}
