namespace EagleEye.Service;

/// <summary>
/// File-system locations of the service's data. The folders are created on start; their ACLs
/// are set by the installer (ADR-008 §7).
/// </summary>
public sealed class ServicePaths : IServicePaths
{
    /// <summary>
    /// Environment variable that overrides the data folder. Honoured in Debug builds only, so
    /// that DEV can run the service unelevated in console mode (US-002 plan, Step 2.4).
    /// </summary>
    public const string DataDirectoryOverrideVariable = "EAGLEEYE_DATA_DIR";

    private const string ProductFolder = "EagleEye";
    private const string CertificateFolder = "certs";
    private const string CertificateFileName = "eagleeye.pfx";
    private const string DatabaseFileName = "EagleEye.Service.db";

    /// <summary>Creates the paths below the given data folder.</summary>
    public ServicePaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = dataDirectory;
        CertificateDirectory = Path.Combine(dataDirectory, CertificateFolder);
        CertificatePath = Path.Combine(CertificateDirectory, CertificateFileName);
        DatabasePath = Path.Combine(dataDirectory, DatabaseFileName);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string CertificateDirectory { get; }

    /// <inheritdoc />
    public string CertificatePath { get; }

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <summary>
    /// Returns the production paths (<c>%ProgramData%\EagleEye</c>), or in Debug builds the
    /// folder named by <see cref="DataDirectoryOverrideVariable"/> if it is set.
    /// </summary>
    public static ServicePaths Resolve()
    {
#if DEBUG
        var overrideDirectory = Environment.GetEnvironmentVariable(DataDirectoryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return new ServicePaths(Path.GetFullPath(overrideDirectory));
        }
#endif
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new ServicePaths(Path.Combine(programData, ProductFolder));
    }

    /// <inheritdoc />
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(CertificateDirectory);
    }
}
