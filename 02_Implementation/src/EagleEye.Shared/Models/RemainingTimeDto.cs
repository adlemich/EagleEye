namespace EagleEye.Shared.Models;

/// <summary>The remaining-time model of one session, pushed to its tray client every engine tick (US-010, ADR-022 §6).</summary>
/// <param name="Lines">
/// All lines with seconds ≤ <see cref="Constants.OverlayRules.MaxThresholdMinutes"/> × 60, unsorted; empty = nothing near.
/// </param>
/// <param name="EarlyMinutes">Early-warning minutes (for the tray's local level as it counts down).</param>
/// <param name="FinalMinutes">Final-warning minutes.</param>
/// <param name="Signal">The strongest level reached in this tick (ledger-checked), or <see cref="OverlaySignal.None"/>.</param>
public sealed record RemainingTimeDto(IReadOnlyList<OverlayLineDto> Lines, int EarlyMinutes, int FinalMinutes, OverlaySignal Signal);
