using EagleEye.Service.Groups;
using EagleEye.Service.Rules;

namespace EagleEye.Service.Enforcement;

/// <summary>What a limit subject is (ADR-021 §4).</summary>
public enum LimitSubjectKind
{
    /// <summary>An ungrouped app with its own rule (US-006).</summary>
    App,

    /// <summary>An app group with its group rule (US-008).</summary>
    Group,
}

/// <summary>
/// What limits a running or starting app (ADR-021 §4, US-008 Decision 5): its app group (group rule of the account, group used
/// time, group name), or the app itself (own rule or the default, app used time, display name).
/// </summary>
/// <param name="Kind">App or group.</param>
/// <param name="Key"><c>app|PATH</c> or <c>group|{id}</c> (upper case), unique per account.</param>
/// <param name="Name">The name in kid messages and the log: the display name or the group name.</param>
/// <param name="GroupId">The group, or <c>null</c> for an app subject.</param>
/// <param name="Rule">The rule that applies.</param>
/// <param name="UsedSeconds">Used time today: of the app, or of the group (union of the current members' spans).</param>
/// <param name="MemberPaths">The app's path, or the group's member paths.</param>
public sealed record LimitSubject(
    LimitSubjectKind Kind, string Key, string Name, long? GroupId, LimitRule Rule, long UsedSeconds, IReadOnlyList<string> MemberPaths);

/// <summary>Resolves the limit subject of an app (US-008 Decision 5). Reads the ledger; otherwise pure.</summary>
public static class LimitSubjectResolver
{
    /// <summary>The group subject when the path is a member of a group, else the app subject.</summary>
    public static LimitSubject Resolve(
        TodayUsageLedger ledger, string accountSid, string programPath, string displayName, AccountAppRules apps, AppGroupsSnapshot groups, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(groups);
        if (groups.GroupOf(programPath) is { } groupId)
        {
            var members = groups.MembersOf(groupId);
            return new LimitSubject(
                LimitSubjectKind.Group, $"group|{groupId}", groups.Names[groupId], groupId, groups.RuleFor(groupId, accountSid),
                ledger.GroupUsedSeconds(accountSid, members, day), members);
        }

        var own = apps.ByPath.GetValueOrDefault(programPath);
        return new LimitSubject(
            LimitSubjectKind.App, $"app|{programPath.ToUpperInvariant()}", displayName, null,
            own is null ? LimitRule.Default : new LimitRule(own.BudgetMinutes, own.Days), ledger.UsedSeconds(accountSid, programPath, day), [programPath]);
    }
}
