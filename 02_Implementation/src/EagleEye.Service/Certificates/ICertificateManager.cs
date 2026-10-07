using System.Security.Cryptography.X509Certificates;

namespace EagleEye.Service.Certificates;

/// <summary>Provides the service's TLS certificate for the parent endpoint (ADR-008 §2).</summary>
public interface ICertificateManager
{
    /// <summary>
    /// Loads the stored certificate, or creates, protects and stores a new one if it is missing
    /// or unreadable.
    /// </summary>
    X509Certificate2 GetOrCreate();
}
