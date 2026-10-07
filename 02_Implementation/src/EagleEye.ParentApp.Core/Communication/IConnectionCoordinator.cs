namespace EagleEye.ParentApp.Core.Communication;

/// <summary>The parent app's connection and pairing state machine.</summary>
public interface IConnectionCoordinator
{
    /// <summary>Current state snapshot.</summary>
    ConnectionState State { get; }

    /// <summary>Raised on every state change, on a thread-pool thread.</summary>
    event Action<ConnectionState>? StateChanged;

    /// <summary>Loads the stored pairing and, if there is one, starts connecting in the background.</summary>
    Task<ConnectionState> InitializeAsync();

    /// <summary>Connects to the host and requests a pairing code (only when not paired).</summary>
    Task BeginPairingAsync(string? hostInput);

    /// <summary>Requests a new pairing code (only while waiting for a code).</summary>
    Task RequestNewCodeAsync();

    /// <summary>Submits code and device name (only while waiting for a code).</summary>
    Task SubmitCodeAsync(string? code, string? deviceName);

    /// <summary>Abandons the pairing in progress.</summary>
    Task CancelPairingAsync();

    /// <summary>De-registers this device at the service and forgets the pairing (only when connected).</summary>
    /// <returns><c>true</c> if the pairing was removed.</returns>
    Task<bool> RemovePairingAsync();
}
