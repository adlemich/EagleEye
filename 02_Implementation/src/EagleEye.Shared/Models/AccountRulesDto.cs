namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the state area "AccountRules:{AccountSid}" (ADR-010 §3).</summary>
/// <param name="Revision">Strictly increasing during one service run.</param>
/// <param name="LastChangeRequestId">requestId of the write that produced this revision, or null.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="BreakTimes">Entries in creation order.</param>
/// <param name="DisplayText">The account's text, or the default text (AC-15); line breaks "\n".</param>
/// <param name="IsDefaultDisplayText">True if the parent never changed the text (or reset it).</param>
public sealed record AccountRulesDto(
    long Revision,
    Guid? LastChangeRequestId,
    string AccountSid,
    IReadOnlyList<BreakTimeEntryDto> BreakTimes,
    string DisplayText,
    bool IsDefaultDisplayText);
