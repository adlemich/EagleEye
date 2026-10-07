namespace EagleEye.Shared.Contracts;

/// <summary>
/// Client-side callback interface for parent apps. The service invokes these methods on
/// connected, paired parent apps. US-002 does not need any server push yet; the interface is
/// the typed-hub contract placeholder. Broadcasts start with device management (FR-APP-015).
/// </summary>
public interface IParentClientCallback
{
}
