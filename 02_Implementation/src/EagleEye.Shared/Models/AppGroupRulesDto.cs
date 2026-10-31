namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the state area "AppGroupRules:{AccountSid}" (ADR-010, ADR-021 §5): a row for every group.</summary>
/// <param name="Revision">Strictly increasing during one service run (shared counter with "AppGroups").</param>
/// <param name="LastChangeRequestId">requestId of the write that produced this revision for this account, or null.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="Rules">One rule per group, unsorted.</param>
public sealed record AppGroupRulesDto(long Revision, Guid? LastChangeRequestId, string AccountSid, IReadOnlyList<AppGroupRuleDto> Rules);
