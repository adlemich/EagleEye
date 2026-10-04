using System.Security.Cryptography.X509Certificates;

namespace EagleEye.Service.Certificates;

/// <summary>Creates the service's self-signed TLS certificate (FR-SVC-060 to FR-SVC-062).</summary>
public interface ISelfSignedCertificateFactory
{
    /// <summary>Creates a new certificate with a private key, valid from <paramref name="now"/>.</summary>
    /// <param name="machineDnsName">DNS name of this machine, added as subject alternative name.</param>
    /// <param name="now">The current time.</param>
    X509Certificate2 Create(string machineDnsName, DateTimeOffset now);
}
