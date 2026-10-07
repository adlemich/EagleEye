namespace EagleEye.Service.Communication;

/// <summary>Shows a pairing code at the service PC (FR-SVC-091, FR-SVC-092).</summary>
public interface IPairingCodeNotifier
{
    /// <summary>Sends the code to all tray clients, or to the Event Log if none is connected.</summary>
    Task NotifyAsync(string code);
}
