namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the global state area "WarningSettings" (FR-APP-054, ADR-015 §8).</summary>
/// <param name="Revision">Strictly increasing during one service run.</param>
/// <param name="LastChangeRequestId">requestId of the write that produced this revision, or null.</param>
/// <param name="EarlyMinutes">Minutes before the end of the early warning (1 … 59, greater than <paramref name="FinalMinutes"/>).</param>
/// <param name="EarlyText">The parent's text, or null for the default (ADR-016).</param>
/// <param name="FinalMinutes">Minutes before the end of the final warning (1 … 58).</param>
/// <param name="FinalText">The parent's text, or null for the default.</param>
public sealed record WarningSettingsDto(
    long Revision,
    Guid? LastChangeRequestId,
    int EarlyMinutes,
    string? EarlyText,
    int FinalMinutes,
    string? FinalText);
