namespace EagleEye.ParentApp.Core.Communication;

/// <summary>Creates real <see cref="ParentHubClient"/>s.</summary>
public sealed class ParentHubClientFactory : IParentHubClientFactory
{
    /// <inheritdoc />
    public IParentHubClient Create(HostAddress host, string? token, CertificateTrustPolicy trust)
    {
        return new ParentHubClient(host, token, trust);
    }
}
