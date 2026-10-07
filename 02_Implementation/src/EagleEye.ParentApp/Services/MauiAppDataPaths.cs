using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>
/// App data in <c>LocalApplicationData\EagleEye</c> (Windows: <c>%LocalAppData%\EagleEye</c>,
/// coding guidelines §16.2). The uninstaller removes this folder (US-002 AC-5).
/// </summary>
public sealed class MauiAppDataPaths : IAppDataPaths
{
    private const string ProductFolder = "EagleEye";
    private const string DatabaseFileName = "EagleEye.ParentApp.db";

    /// <summary>Resolves the paths and creates the folder.</summary>
    public MauiAppDataPaths()
    {
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder);
        DatabasePath = Path.Combine(DataDirectory, DatabaseFileName);
        Directory.CreateDirectory(DataDirectory);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string DatabasePath { get; }
}
