using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>Data access for <c>AppGroups</c>, <c>AppGroupMembers</c> and <c>AppGroupRules</c> (US-008, ADR-021 §1).</summary>
public interface IAppGroupRepository
{
    /// <summary>Loads all groups, members, rules and the app catalog (all app records).</summary>
    Task<StoredAppGroups> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Creates a group and returns its id. A duplicate name key throws (the caller checks first).</summary>
    Task<long> CreateGroupAsync(string name, string nameKey, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Renames a group; <c>false</c> if it does not exist.</summary>
    Task<bool> RenameGroupAsync(long groupId, string name, string nameKey, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Deletes a group with its members and rules (cascade); <c>false</c> if it does not exist.</summary>
    Task<bool> DeleteGroupAsync(long groupId, CancellationToken ct = default);

    /// <summary>Puts the program path into the group, or out of every group with <paramref name="groupId"/> = <c>null</c>.</summary>
    Task SetMemberAsync(string programPath, long? groupId, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Stores a group rule; the default rule (0:00, no day) deletes the row.</summary>
    Task SetRuleAsync(long groupId, string accountSid, int budgetMinutes, BreakTimeDays days, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Deletes the group rules of every SID not in <paramref name="existingSids"/>; returns the purged SIDs.</summary>
    Task<IReadOnlyList<string>> PurgeRulesNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
