namespace EagleEye.Shared.Models;

/// <summary>
/// Version information returned by the service.
/// Format: "EagleEye_vMAJOR.MINOR" (e.g., "EagleEye_v0.1").
/// </summary>
/// <param name="Version">The formatted version identifier.</param>
public sealed record ServiceVersionDto(string Version);
