using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>
/// App data folder. Windows: <c>LocalApplicationData\EagleEye</c> (<c>%LocalAppData%\EagleEye</c>, coding guidelines
/// §16.2; the uninstaller removes it, US-002 AC-5). macOS: <c>~/Library/Application Support/EagleEye</c> (ADR-020 §1,
/// US-007 Decision 4), resolved through <c>NSFileManager</c>; Debug builds on the Mac use <c>EagleEye-Debug</c> so a dev
/// build never touches the installed app's data (US-007 Step 1 review R-2).
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

#if MACCATALYST && DEBUG
    private const string ProductFolder = "EagleEye-Debug";
#else
    private const string ProductFolder = "EagleEye";
#endif
    private const string DatabaseFileName = "EagleEye.ParentApp.db";

    /// <summary>Resolves the paths and creates the folder.</summary>
    public MauiAppDataPaths()
    {
        DataDirectory = Path.Combine(PlatformDataRoot(), ProductFolder);
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

    private static string PlatformDataRoot()
    {
#if MACCATALYST
        // Application Support of the user (not Environment.SpecialFolder, whose Catalyst mapping differs between .NET versions).
        var url = Foundation.NSFileManager.DefaultManager.GetUrl(
            Foundation.NSSearchPathDirectory.ApplicationSupportDirectory, Foundation.NSSearchPathDomain.User, null, true, out var error);
        return url?.Path ?? throw new IOException($"Application Support folder not available: {error?.LocalizedDescription}");
#else
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
    }
}
