namespace EagleEye.Service;

/// <summary>
/// File-system locations of the service's data (ADR-008 §2, coding guidelines §12.1).
/// </summary>
public interface IServicePaths
{
    /// <summary>Root data folder, normally <c>%ProgramData%\EagleEye</c>.</summary>
    string DataDirectory { get; }

    /// <summary>Folder of the TLS certificate (<c>certs</c>, restricted by the installer's ACL).</summary>
    string CertificateDirectory { get; }

    /// <summary>Full path of the protected PFX file.</summary>
    string CertificatePath { get; }

    /// <summary>Full path of the service database.</summary>
    string DatabasePath { get; }

    /// <summary>Folder of the service log files (<c>logs</c>, SYSTEM and Administrators only, US-003 AC-14).</summary>
    string LogDirectory { get; }

    /// <summary>File name prefix of the service log files (<c>EagleEye.Service-NNN.log</c>).</summary>
    string LogFilePrefix { get; }

    /// <summary>Whether the Debug data-folder override (<c>EAGLEEYE_DATA_DIR</c>) is active.</summary>
    bool IsOverridden { get; }

    /// <summary>Creates the data, certificate and log folders if they do not exist.</summary>
    void EnsureDirectories();
}
