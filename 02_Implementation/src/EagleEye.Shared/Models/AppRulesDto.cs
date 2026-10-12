namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the state area "AppRules:{AccountSid}" (ADR-010, ADR-015 §8).</summary>
/// <param name="Revision">Strictly increasing during one service run.</param>
/// <param name="LastChangeRequestId">requestId of the write that produced this revision, or null.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="Apps">Unsorted; the parent app sorts by display name in its culture (AC-2).</param>
/// <param name="BudgetText">The parent's budget text, or null for the default (ADR-016); line breaks "\n".</param>
public sealed record AppRulesDto(
    long Revision,
    Guid? LastChangeRequestId,
    string AccountSid,
    IReadOnlyList<AppRuleDto> Apps,
    string? BudgetText);
