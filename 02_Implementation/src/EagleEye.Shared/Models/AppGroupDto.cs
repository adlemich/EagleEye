namespace EagleEye.Shared.Models;

/// <summary>An app group of the service PC (US-008, FR-APP-042).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Name">The name, normalized by <see cref="Constants.AppGroupNames.Normalize"/>.</param>
public sealed record AppGroupDto(long GroupId, string Name);
