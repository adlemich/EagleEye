namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// Called by <see cref="ConnectionCoordinator"/> when the paired connection is confirmed or lost.
/// Implemented by <see cref="ParentHubGateway"/>. Public only so that the MAUI head can register the
/// gateway under this interface; feature code uses <see cref="IParentHubGateway"/>.
/// </summary>
public interface IPairedConnectionSink
{
    /// <summary>The client is connected and the service confirmed the pairing.</summary>
    void SetConnected(IParentHubClient client);

    /// <summary>The paired connection is lost, reconnecting, or stopped.</summary>
    void SetDisconnected();
}
