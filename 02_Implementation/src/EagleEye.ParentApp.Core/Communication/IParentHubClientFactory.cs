namespace EagleEye.ParentApp.Core.Communication;

/// <summary>Creates parent hub connections; lets the coordinator be tested with mocked clients.</summary>
public interface IParentHubClientFactory
{
    /// <summary>
    /// Creates a connection to the host. With a <paramref name="token"/>, the connection
    /// authenticates as a paired device and reconnects automatically; without, it is a one-off
    /// pairing connection (a reconnect would get a new connection and lose the pending code).
    /// </summary>
    IParentHubClient Create(HostAddress host, string? token, CertificateTrustPolicy trust);
}
