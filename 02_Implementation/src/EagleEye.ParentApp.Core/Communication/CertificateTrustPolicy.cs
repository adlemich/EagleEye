using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// Server-certificate trust for one connection (ADR-008 §3): trust on first use while pairing
/// (accept and remember the SHA-256 thumbprint), afterwards accept only the pinned thumbprint.
/// Certificates are never installed into an OS store. Thread-safe: TLS callbacks run on
/// thread-pool threads.
/// </summary>
public sealed class CertificateTrustPolicy
{
    private volatile string? _observedThumbprint;
    private volatile bool _pinMismatch;

    private CertificateTrustPolicy(string? pinnedThumbprint)
    {
        PinnedThumbprint = pinnedThumbprint;
    }

    /// <summary>The pinned thumbprint, or <c>null</c> in trust-on-first-use mode.</summary>
    public string? PinnedThumbprint { get; }

    /// <summary>SHA-256 thumbprint (upper-case hex) of the last certificate presented by the server.</summary>
    public string? ObservedThumbprint => _observedThumbprint;

    /// <summary>Whether a certificate different from the pinned one was presented.</summary>
    public bool PinMismatch => _pinMismatch;

    /// <summary>Accept any certificate and capture its thumbprint (pairing).</summary>
    public static CertificateTrustPolicy TrustOnFirstUse() => new(pinnedThumbprint: null);

    /// <summary>Accept only a certificate with this SHA-256 thumbprint (hex, case-insensitive).</summary>
    public static CertificateTrustPolicy Pinned(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        return new CertificateTrustPolicy(thumbprint);
    }

    /// <summary>The TLS validation decision for the presented certificate.</summary>
    public bool Validate(X509Certificate? certificate)
    {
        if (certificate is null)
        {
            return false;
        }

        var thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        _observedThumbprint = thumbprint;

        if (PinnedThumbprint is null || string.Equals(PinnedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _pinMismatch = true;
        return false;
    }
}
