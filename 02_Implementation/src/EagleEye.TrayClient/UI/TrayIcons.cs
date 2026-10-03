using System.Reflection;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Loads the tray icons embedded in the assembly (UI/Resources/*.ico).
/// </summary>
internal static class TrayIcons
{
    private const string ResourcePrefix = "EagleEye.TrayClient.UI.Resources.";

    public const string Connected = "eagleeye-connected.ico";
    public const string Disconnected = "eagleeye-disconnected.ico";
    public const string Application = "eagleeye.ico";

    /// <summary>Loads the named icon in the requested size.</summary>
    public static Icon Load(string fileName, Size size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Embedded icon '{fileName}' not found.");
        return new Icon(stream, size);
    }
}
