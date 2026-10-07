namespace EagleEye.Service;

/// <summary>
/// File-system locations of the service's data. The folders are created on start; their ACLs
/// are set by the installer (ADR-008 §7), and the ACL of <c>logs\</c> again by the service on
/// every start (<see cref="Diagnostics.LogDirectoryProtector"/>).
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
    private const string LogFolder = "logs";
    private const string ServiceLogFilePrefix = "EagleEye.Service";

    /// <summary>Creates the paths below the given data folder.</summary>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="isOverridden">Whether the folder comes from the Debug override.</param>
    public ServicePaths(string dataDirectory, bool isOverridden = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = dataDirectory;
        CertificateDirectory = Path.Combine(dataDirectory, CertificateFolder);
        CertificatePath = Path.Combine(CertificateDirectory, CertificateFileName);
        DatabasePath = Path.Combine(dataDirectory, DatabaseFileName);
        LogDirectory = Path.Combine(dataDirectory, LogFolder);
        IsOverridden = isOverridden;
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string CertificateDirectory { get; }

    /// <inheritdoc />
    public string CertificatePath { get; }

    /// <inheritdoc />
    public string DatabasePath { get; }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public string LogFilePrefix => ServiceLogFilePrefix;

    /// <inheritdoc />
    public bool IsOverridden { get; }

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
            return new ServicePaths(Path.GetFullPath(overrideDirectory), isOverridden: true);
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
        Directory.CreateDirectory(LogDirectory);
    }
}
