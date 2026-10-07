using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EagleEye.Service.Certificates;

/// <summary>
/// Creates the ECDSA P-256 certificate <c>CN=EagleEye</c> with the machine DNS name and
/// <c>localhost</c> as subject alternative names, server-authentication EKU, valid 100 years
/// (ADR-008 §2).
/// </summary>
public sealed class SelfSignedCertificateFactory : ISelfSignedCertificateFactory
{
    /// <summary>Subject of the certificate.</summary>
    public const string SubjectName = "CN=EagleEye";

    /// <summary>Validity period in years.</summary>
    public const int ValidityYears = 100;

    private const string LocalhostName = "localhost";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>Clock-skew allowance: the certificate is valid from one day before creation.</summary>
    private static readonly TimeSpan BackdateBy = TimeSpan.FromDays(1);

    /// <inheritdoc />
    public X509Certificate2 Create(string machineDnsName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineDnsName);

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(SubjectName, key, HashAlgorithmName.SHA256);

        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(machineDnsName);
        alternativeNames.AddDnsName(LocalhostName);
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid(ServerAuthenticationOid)], false));

        return request.CreateSelfSigned(now - BackdateBy, now.AddYears(ValidityYears));
    }
}
