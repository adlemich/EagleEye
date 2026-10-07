namespace EagleEye.ParentApp.Core.Communication;

/// <summary>Immutable snapshot of the coordinator state. Never contains the token.</summary>
/// <param name="Status">The connection status.</param>
/// <param name="Host">The host as entered by the parent; when not paired, the host to prefill.</param>
/// <param name="DeviceName">The device name of the pairing, if paired.</param>
/// <param name="LastMessage">The last error or information.</param>
public sealed record ConnectionState(ConnectionStatus Status, string? Host, string? DeviceName, ConnectionMessage LastMessage)
{
    /// <summary>Not paired, no host, no message.</summary>
    public static ConnectionState Initial { get; } = new(ConnectionStatus.NotPaired, null, null, ConnectionMessage.None);

    /// <summary>The pairing status derived from <see cref="Status"/>.</summary>
    public PairingStatus PairingStatus => Status switch
    {
        ConnectionStatus.NotPaired => PairingStatus.NotPaired,
        ConnectionStatus.PairingConnecting or ConnectionStatus.AwaitingCode => PairingStatus.InProgress,
        _ => PairingStatus.Paired,
    };
}
