namespace EagleEye.Service.Data;

/// <summary>A paired parent device as stored by the service. Holds the token hash, never the token.</summary>
/// <param name="DeviceId">The device ID (GUID, "D" format).</param>
/// <param name="DeviceName">The device name given at pairing.</param>
/// <param name="TokenHash">SHA-256 hash of the device's token.</param>
/// <param name="PairedAtUtc">When the device was paired.</param>
public sealed record PairedDevice(string DeviceId, string DeviceName, byte[] TokenHash, DateTimeOffset PairedAtUtc);
