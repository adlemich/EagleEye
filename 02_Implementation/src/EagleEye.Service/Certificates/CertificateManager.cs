using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EagleEye.Service.Certificates;

/// <summary>
/// Loads <c>certs\eagleeye.pfx</c> or creates it on first start (ADR-008 §2). The PFX bytes are
/// encrypted with DPAPI (<see cref="DataProtectionScope.LocalMachine"/>); the folder ACL set by the
/// installer is the actual protection. The key is loaded with
/// <see cref="X509KeyStorageFlags.MachineKeySet"/>: SChannel cannot use an ephemeral key for a TLS
/// server. File and DPAPI behaviour is verified manually (US-002 plan).
/// </summary>
public sealed class CertificateManager(
    IServicePaths paths,
    ISelfSignedCertificateFactory factory,
    TimeProvider timeProvider,
    ILogger<CertificateManager> logger) : ICertificateManager
{
    private readonly Lock _lock = new();
    private X509Certificate2? _certificate;

    /// <inheritdoc />
    public X509Certificate2 GetOrCreate()
    {
        lock (_lock)
        {
            _certificate ??= TryLoad() ?? CreateAndSave();
            return _certificate;
        }
    }

    private X509Certificate2? TryLoad()
    {
        if (!File.Exists(paths.CertificatePath))
        {
            logger.LogInformation("No TLS certificate found; a new one is created.");
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(paths.CertificatePath);
            var pfx = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.LocalMachine);
            var certificate = Import(pfx);
            logger.LogInformation("TLS certificate loaded, thumbprint {Thumbprint}.", certificate.GetCertHashString(HashAlgorithmName.SHA256));
            return certificate;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The stored TLS certificate is unreadable; a new one is created. Paired parent apps must be paired again.");
            return null;
        }
    }

    private X509Certificate2 CreateAndSave()
    {
        using var created = factory.Create(System.Net.Dns.GetHostName(), timeProvider.GetUtcNow());
        var pfx = created.Export(X509ContentType.Pkcs12);
        var protectedBytes = ProtectedData.Protect(pfx, optionalEntropy: null, DataProtectionScope.LocalMachine);

        Directory.CreateDirectory(paths.CertificateDirectory);
        File.WriteAllBytes(paths.CertificatePath, protectedBytes);

        var certificate = Import(pfx);
        logger.LogInformation("TLS certificate created, thumbprint {Thumbprint}.", certificate.GetCertHashString(HashAlgorithmName.SHA256));
        return certificate;
    }

    private X509Certificate2 Import(byte[] pfx)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.MachineKeySet);
        }
        catch (CryptographicException ex)
        {
            // Without admin rights (console mode for development) the machine key store is not
            // writable. The user key store also works for SChannel; SYSTEM never gets here.
            logger.LogWarning(ex, "The machine key store is not accessible; the certificate key is loaded into the user key store.");
            return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.UserKeySet);
        }
    }
}
