using System.Collections.Immutable;
using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;

namespace EagleEye.Service.Groups;

/// <summary>Read access to the current app groups (lock-free), for enforcement and Reports (ADR-021 §3, §4).</summary>
public interface IAppGroupsView
{
    /// <summary>The current groups, members and group rules.</summary>
    AppGroupsSnapshot Current { get; }
}

/// <summary>
/// Immutable copy of the app groups of the service PC (US-008 Decision 4): names, memberships (program path → group,
/// case-insensitive) and group rules (group, SID → rule; no entry = 0:00, no day). Replaced as a whole on every change.
/// </summary>
public sealed class AppGroupsSnapshot
{
    private AppGroupsSnapshot(
        ImmutableDictionary<long, string> names, ImmutableDictionary<string, long> members, ImmutableDictionary<(long, string), LimitRule> rules)
    {
        Names = names;
        MembersByPath = members;
        Rules = rules;
    }

    /// <summary>No groups.</summary>
    public static AppGroupsSnapshot Empty { get; } = new(
        ImmutableDictionary<long, string>.Empty,
        ImmutableDictionary.Create<string, long>(StringComparer.OrdinalIgnoreCase),
        ImmutableDictionary<(long, string), LimitRule>.Empty);

    /// <summary>The group names by id.</summary>
    public ImmutableDictionary<long, string> Names { get; }

    /// <summary>The group of each member path.</summary>
    public ImmutableDictionary<string, long> MembersByPath { get; }

    /// <summary>The stored group rules by (group, upper-case SID).</summary>
    public ImmutableDictionary<(long GroupId, string SidUpper), LimitRule> Rules { get; }

    /// <summary>Builds the snapshot from the stored rows.</summary>
    public static AppGroupsSnapshot From(StoredAppGroups stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return new AppGroupsSnapshot(
            stored.Groups.ToImmutableDictionary(g => g.GroupId, g => g.Name),
            stored.Members.ToImmutableDictionary(m => m.ProgramPath, m => m.GroupId, StringComparer.OrdinalIgnoreCase),
            stored.Rules.ToImmutableDictionary(r => (r.GroupId, r.AccountSid.ToUpperInvariant()), r => new LimitRule(r.BudgetMinutes, r.Days)));
    }

    /// <summary>The group of the program path, or <c>null</c>.</summary>
    public long? GroupOf(string programPath)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return MembersByPath.TryGetValue(programPath, out var id) ? id : null;
    }

    /// <summary>The member paths of the group.</summary>
    public IReadOnlyList<string> MembersOf(long groupId) => [.. MembersByPath.Where(m => m.Value == groupId).Select(m => m.Key)];

    /// <summary>The group rule of the account; the default rule (0:00, no day) when nothing is stored.</summary>
    public LimitRule RuleFor(long groupId, string accountSid)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        return Rules.GetValueOrDefault((groupId, accountSid.ToUpperInvariant())) ?? LimitRule.Default;
    }

    /// <summary>A copy with a group added or renamed.</summary>
    public AppGroupsSnapshot WithGroup(long groupId, string name) => new(Names.SetItem(groupId, name), MembersByPath, Rules);

    /// <summary>A copy without the group, its members and its rules.</summary>
    public AppGroupsSnapshot WithoutGroup(long groupId) => new(
        Names.Remove(groupId),
        MembersByPath.RemoveRange(MembersByPath.Where(m => m.Value == groupId).Select(m => m.Key)),
        Rules.RemoveRange(Rules.Keys.Where(k => k.GroupId == groupId)));

    /// <summary>A copy with the path in the group, or in no group (<c>null</c>).</summary>
    public AppGroupsSnapshot WithMember(string programPath, long? groupId) =>
        new(Names, groupId is { } id ? MembersByPath.SetItem(programPath, id) : MembersByPath.Remove(programPath), Rules);

    /// <summary>A copy with the account's group rule (the default rule removes the entry).</summary>
    public AppGroupsSnapshot WithRule(long groupId, string accountSid, LimitRule rule)
    {
        var key = (groupId, accountSid.ToUpperInvariant());
        return new(Names, MembersByPath, rule == LimitRule.Default ? Rules.Remove(key) : Rules.SetItem(key, rule));
    }

    /// <summary>A copy without the rules of the accounts.</summary>
    public AppGroupsSnapshot WithoutRulesOf(IReadOnlyCollection<string> accountSids)
    {
        var gone = accountSids.Select(s => s.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        return new(Names, MembersByPath, Rules.RemoveRange(Rules.Keys.Where(k => gone.Contains(k.SidUpper))));
    }
}
