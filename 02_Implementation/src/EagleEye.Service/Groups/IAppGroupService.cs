using EagleEye.Shared.Models;

namespace EagleEye.Service.Groups;

/// <summary>
/// State owner of the areas "AppGroups" and "AppGroupRules:{sid}" (ADR-010 §9, ADR-021 §5, US-008 Decision 4). Writes take
/// the client's <c>requestId</c> and the sender's device name (for the log); one revision counter for both areas.
/// </summary>
public interface IAppGroupService : IAppGroupsView
{
    /// <summary>Loads groups, members, rules and the app catalog (service start, before the accounting loop runs).</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>The groups and the listed apps.</summary>
    Task<AppGroupsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Creates an empty group (AC-7).</summary>
    /// <exception cref="ArgumentException">The name breaks the rules of <c>AppGroupNames</c>.</exception>
    /// <exception cref="Rules.DuplicateGroupNameException">Another group has the name, ignoring case.</exception>
    Task<StateWriteAckDto> CreateAsync(Guid requestId, string name, string deviceName, CancellationToken ct = default);

    /// <summary>Renames a group (AC-9).</summary>
    /// <exception cref="Rules.GroupNotFoundException">The group does not exist.</exception>
    Task<StateWriteAckDto> RenameAsync(Guid requestId, long groupId, string name, string deviceName, CancellationToken ct = default);

    /// <summary>Deletes a group with its members and rules (AC-10).</summary>
    /// <exception cref="Rules.GroupNotFoundException">The group does not exist.</exception>
    Task<StateWriteAckDto> DeleteAsync(Guid requestId, long groupId, string deviceName, CancellationToken ct = default);

    /// <summary>Moves a listed app into a group or out of every group (AC-11 to AC-13).</summary>
    /// <exception cref="Rules.AppNotFoundException">The app is not listed.</exception>
    /// <exception cref="Rules.GroupNotFoundException">The target group does not exist.</exception>
    Task<StateWriteAckDto> MoveAsync(Guid requestId, string programPath, long? groupId, string deviceName, CancellationToken ct = default);

    /// <summary>The group rules of one account: a row for every group.</summary>
    /// <exception cref="UserAccounts.UnknownAccountException">The SID is not a standard account of the inventory.</exception>
    Task<AppGroupRulesDto> GetRulesAsync(string accountSid, CancellationToken ct = default);

    /// <summary>Sets the hours or minutes of a group's budget, combined with the stored other part (AC-16).</summary>
    Task<StateWriteAckDto> SetBudgetAsync(
        Guid requestId, string accountSid, long groupId, AppBudgetPart part, int value, string deviceName, CancellationToken ct = default);

    /// <summary>Ticks or unticks one weekday of a group rule (AC-16).</summary>
    Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long groupId, DayOfWeek day, bool isAllowed, string deviceName, CancellationToken ct = default);

    /// <summary>The accounting loop's set of controlled accounts (each tick); broadcasts only when the listed apps change (TI-5).</summary>
    Task OnControlledAccountsAsync(IReadOnlyCollection<string> accountSids, CancellationToken ct = default);
}
