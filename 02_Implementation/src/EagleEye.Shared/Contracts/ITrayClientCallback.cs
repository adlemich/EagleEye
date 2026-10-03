namespace EagleEye.Shared.Contracts;

/// <summary>
/// Client-side callback interface for tray clients.
/// The service invokes these methods on connected tray clients.
/// US-001 does not require any server-push callbacks; the interface is the typed-hub
/// contract placeholder. Budget updates, warnings and pairing codes follow in later stories.
/// </summary>
public interface ITrayClientCallback
{
}
