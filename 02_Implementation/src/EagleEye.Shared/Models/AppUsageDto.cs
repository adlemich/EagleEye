namespace EagleEye.Shared.Models;

/// <summary>Usage of one app on one day (US-004, ADR-012 §6).</summary>
/// <param name="AppId">Stable id of the app record (per account, per program path).</param>
/// <param name="DisplayName">Task Manager's name (AC-8), resolved by the service.</param>
/// <param name="Seconds">Active seconds on that day (FR-SVC-012), ≥ 0.</param>
public sealed record AppUsageDto(long AppId, string DisplayName, long Seconds);
