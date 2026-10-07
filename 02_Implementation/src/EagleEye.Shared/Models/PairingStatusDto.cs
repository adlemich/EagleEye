namespace EagleEye.Shared.Models;

/// <summary>
/// Pairing state of a parent hub connection, returned by <c>IParentHub.GetPairingStatus</c>.
/// </summary>
/// <param name="IsPaired">Whether the connection is authenticated as a paired device.</param>
/// <param name="DeviceId">The device ID, if paired.</param>
/// <param name="DeviceName">The device name, if paired.</param>
public sealed record PairingStatusDto(bool IsPaired, string? DeviceId, string? DeviceName);
