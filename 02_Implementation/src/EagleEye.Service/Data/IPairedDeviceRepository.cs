namespace EagleEye.Service.Data;

/// <summary>Data access for the <c>PairedDevices</c> table.</summary>
public interface IPairedDeviceRepository
{
    /// <summary>Stores a new paired device.</summary>
    Task AddAsync(PairedDevice device, CancellationToken ct = default);

    /// <summary>Returns the device with the given token hash, or <c>null</c>.</summary>
    Task<PairedDevice?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);

    /// <summary>Removes the device; returns whether it existed.</summary>
    Task<bool> RemoveAsync(string deviceId, CancellationToken ct = default);
}
