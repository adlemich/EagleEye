namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Location of the app's data (Windows: <c>%LocalAppData%\EagleEye</c>, coding guidelines §16.2).</summary>
public interface IAppDataPaths
{
    /// <summary>The app data folder.</summary>
    string DataDirectory { get; }

    /// <summary>Full path of <c>EagleEye.ParentApp.db</c>.</summary>
    string DatabasePath { get; }
}
