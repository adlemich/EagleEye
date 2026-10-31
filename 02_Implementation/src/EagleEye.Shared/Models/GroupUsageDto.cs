namespace EagleEye.Shared.Models;

/// <summary>Used time of one group on one day: the union of the current members' activity (TC-026, US-008 AC-35).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Name">The group's current name.</param>
/// <param name="Seconds">Seconds during which at least one member app was active, ≥ 0.</param>
public sealed record GroupUsageDto(long GroupId, string Name, long Seconds);
