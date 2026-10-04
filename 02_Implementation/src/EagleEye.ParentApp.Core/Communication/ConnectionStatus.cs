namespace EagleEye.ParentApp.Core.Communication;

/// <summary>States of the parent app's connection state machine (US-002 plan, coordinator diagram).</summary>
public enum ConnectionStatus
{
    /// <summary>No pairing; the parent can enter a host. Status "Not connected" (red).</summary>
    NotPaired,

    /// <summary>Connecting to a host to start pairing. "Connecting to &lt;host&gt; …" (red).</summary>
    PairingConnecting,

    /// <summary>Connected, code requested; waiting for code and device name. "Connecting to &lt;host&gt; …" (red).</summary>
    AwaitingCode,

    /// <summary>Paired; connecting with the token. "Connecting to &lt;host&gt; …" (red).</summary>
    PairedConnecting,

    /// <summary>Paired and connected, confirmed by the service. "Connected to &lt;host&gt;" (green).</summary>
    PairedConnected,

    /// <summary>Paired but not connected; retrying. "Not connected to &lt;host&gt;" (red).</summary>
    PairedDisconnected,
}
