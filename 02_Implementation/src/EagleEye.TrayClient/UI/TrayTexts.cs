using System.Globalization;
using System.Resources;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Localized user-facing texts of the tray client (NFR-L-010 to NFR-L-012).
/// German is the neutral/default language (<c>Resources/TrayTexts.resx</c>); English is in
/// <c>Resources/TrayTexts.en.resx</c>. The current UI culture selects the language, and any
/// language other than English falls back to German.
/// </summary>
internal static class TrayTexts
{
    /// <summary>Resource manager for the tray texts (exposed for completeness tests).</summary>
    internal static readonly ResourceManager ResourceManager =
        new("EagleEye.TrayClient.Resources.TrayTexts", typeof(TrayTexts).Assembly);

    public static string TooltipConnected => Get(nameof(TooltipConnected));

    public static string TooltipDisconnected => Get(nameof(TooltipDisconnected));

    public static string AboutMenuItem => Get(nameof(AboutMenuItem));

    public static string AboutTitle => Get(nameof(AboutTitle));

    /// <summary>Composite format with one placeholder for the version string.</summary>
    public static string ServerVersionFormat => Get(nameof(ServerVersionFormat));

    /// <summary>Composite format with one placeholder for the server address (host:port).</summary>
    public static string ConnectionErrorFormat => Get(nameof(ConnectionErrorFormat));

    public static string Ok => Get(nameof(Ok));

    /// <summary>Title of the pairing-code window (FR-TRAY-070).</summary>
    public static string PairingTitle => Get(nameof(PairingTitle));

    /// <summary>Composite format with one placeholder for the 6-digit pairing code.</summary>
    public static string PairingCodeFormat => Get(nameof(PairingCodeFormat));

    public static string PairingInstruction => Get(nameof(PairingInstruction));

    /// <summary>Composite format with one placeholder for the validity in minutes.</summary>
    public static string PairingValidityFormat => Get(nameof(PairingValidityFormat));

    /// <summary>Title of the break-time message (FR-TRAY-022, AC-30); "EagleEye" in every language.</summary>
    public static string BreakMessageTitle => Get(nameof(BreakMessageTitle));

    /// <summary>Returns the text for the current UI culture.</summary>
    /// <exception cref="InvalidOperationException">The resource key does not exist.</exception>
    internal static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Missing tray text resource '{name}'.");
    }
}
