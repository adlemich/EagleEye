using System.Globalization;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Composes the body text of the About dialog: the server version (AC-12), or a connection
/// error naming the server address when the version could not be retrieved (AC-13).
/// </summary>
internal static class AboutText
{
    /// <summary>Returns the localized dialog text for the current UI culture.</summary>
    /// <param name="serverVersion">The version string, or <c>null</c> if it could not be retrieved.</param>
    /// <param name="serverAddress">The address the tray client connects to, e.g. "localhost:5080".</param>
    public static string Compose(string? serverVersion, string serverAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAddress);

        return serverVersion is null
            ? string.Format(CultureInfo.CurrentCulture, TrayTexts.ConnectionErrorFormat, serverAddress)
            : string.Format(CultureInfo.CurrentCulture, TrayTexts.ServerVersionFormat, serverVersion);
    }
}
