using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>A stored app group (US-008, migration 6).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Name">The normalized name.</param>
public sealed record StoredAppGroup(long GroupId, string Name);

/// <summary>A stored membership: the program path belongs to the group (one group per path, AC-13).</summary>
/// <param name="ProgramPath">The program path.</param>
/// <param name="GroupId">The group.</param>
public sealed record StoredAppGroupMember(string ProgramPath, long GroupId);

/// <summary>A stored group rule of one account (no row = 0:00, no day).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="BudgetMinutes">The budget.</param>
/// <param name="Days">The allowed weekdays.</param>
public sealed record StoredAppGroupRule(long GroupId, string AccountSid, int BudgetMinutes, BreakTimeDays Days);

/// <summary>An app record as seen by the App Groups catalog (AC-3): account, path, latest display name and when it was seen.</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="ProgramPath">The program path.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="LastSeenUtc">When the record was last seen.</param>
public sealed record StoredCatalogApp(string AccountSid, string ProgramPath, string DisplayName, DateTimeOffset LastSeenUtc);

/// <summary>Everything stored for app groups, plus the app catalog from <c>AppRecords</c>.</summary>
/// <param name="Groups">All groups.</param>
/// <param name="Members">All memberships.</param>
/// <param name="Rules">All stored group rules.</param>
/// <param name="Catalog">All app records.</param>
public sealed record StoredAppGroups(
    IReadOnlyList<StoredAppGroup> Groups,
    IReadOnlyList<StoredAppGroupMember> Members,
    IReadOnlyList<StoredAppGroupRule> Rules,
    IReadOnlyList<StoredCatalogApp> Catalog);
