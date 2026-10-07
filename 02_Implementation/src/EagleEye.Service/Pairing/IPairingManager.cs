using EagleEye.Service.Data;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Pairing;

/// <summary>The pairing protocol of ADR-008 §4: one pending code, bound to a connection.</summary>
public interface IPairingManager
{
    /// <summary>Creates a new code bound to <paramref name="connectionId"/> (replacing any pending code) and shows it.</summary>
    Task StartPairingAsync(string connectionId);

    /// <summary>Validates a submitted code. Every outcome clears the pending code.</summary>
    Task<PairingResultDto> SubmitAsync(string connectionId, string? code, string? deviceName);

    /// <summary>Returns the paired device for the token, or <c>null</c> if the token is empty or unknown.</summary>
    Task<PairedDevice?> AuthenticateAsync(string? token);

    /// <summary>De-registers a device; returns whether it existed.</summary>
    Task<bool> RemoveDeviceAsync(string deviceId);
}
