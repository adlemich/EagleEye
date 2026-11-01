namespace EagleEye.Shared.Models;

/// <summary>One line of the remaining-time overlay (US-010 AC-3, ADR-022 §6).</summary>
/// <param name="Kind">App (ungrouped), group, or the break line.</param>
/// <param name="Name">Display name (app) or group name as the parent named it; null for the break line.</param>
/// <param name="Seconds">
/// Seconds left, rounded up, ≥ 0: own end of the subject (no breaks), the grace left during a grace period, or the seconds
/// until the next break begins (break grace: grace left).
/// </param>
/// <param name="Level">The level of <paramref name="Seconds"/> at the push (<see cref="Constants.OverlayRules.LevelFor"/>).</param>
/// <param name="Counting">
/// Whether the time runs down now: wall-clock lines (break, midnight, grace) always; budget lines while the account is in use.
/// </param>
public sealed record OverlayLineDto(OverlayLineKind Kind, string? Name, int Seconds, OverlayLevel Level, bool Counting);
