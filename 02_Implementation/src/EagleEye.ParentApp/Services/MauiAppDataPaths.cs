using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>
/// App data in <c>LocalApplicationData\EagleEye</c> (Windows: <c>%LocalAppData%\EagleEye</c>,
/// coding guidelines §16.2). The uninstaller removes this folder (US-002 AC-5).
/// </summary>
public sealed class MauiAppDataPaths : IAppDataPaths
{
#if DEBUG
    /// <summary>
    /// <b>Debug builds only</b> (US-005 D-5): another data folder, so a DEV smoke check never reads or changes the data
    /// (pairing, token) of a parent app installed on the same PC. Release builds do not contain the switch.
    /// </summary>
    public const string DevDataDirectoryVariable = "EAGLEEYE_DEV_APP_DATA_DIR";
#endif

    private const string ProductFolder = "EagleEye";
    private const string DatabaseFileName = "EagleEye.ParentApp.db";

    /// <summary>Resolves the paths and creates the folder.</summary>
    public MauiAppDataPaths()
    {
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder);
#if DEBUG
        if (Environment.GetEnvironmentVariable(DevDataDirectoryVariable) is { Length: > 0 } devDirectory)
        {
            DataDirectory = Path.GetFullPath(devDirectory);
        }
#endif
        DatabasePath = Path.Combine(DataDirectory, DatabaseFileName);
        Directory.CreateDirectory(DataDirectory);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string DatabasePath { get; }
}
