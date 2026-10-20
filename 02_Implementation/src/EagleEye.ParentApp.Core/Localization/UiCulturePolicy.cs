using System.Globalization;

namespace EagleEye.ParentApp.Core.Localization;

/// <summary>
/// UI language and formatting culture of the Mac app (NFR-L-011, US-007 AC-13, AC-15, ADR-020 §4). Pure; called by the
/// macOS platform layer only (Windows keeps the .NET defaults).
/// </summary>
public static class UiCulturePolicy
{
    private const string English = "en";
    private const string German = "de";
    private static readonly char[] SubtagSeparators = ['-', '_'];

    /// <summary>"en" if the first preferred language is English, otherwise "de" (German is the neutral language).</summary>
    /// <param name="firstPreferredLanguage">The first preferred language of the process, e.g. <c>en-US</c> or <c>de-DE</c>.</param>
    public static CultureInfo ResolveUiCulture(string? firstPreferredLanguage)
    {
        var primary = firstPreferredLanguage?.Trim().Split(SubtagSeparators)[0];
        return CultureInfo.GetCultureInfo(string.Equals(primary, English, StringComparison.OrdinalIgnoreCase) ? English : German);
    }

    /// <summary>UI language + region (e.g. de-DE, en-US, en-DE); the UI language alone if the region is missing or the pair is unknown.</summary>
    /// <param name="uiCulture">The UI culture from <see cref="ResolveUiCulture"/>.</param>
    /// <param name="regionCode">The macOS region code, e.g. <c>DE</c> or <c>US</c>.</param>
    public static CultureInfo ResolveFormattingCulture(CultureInfo uiCulture, string? regionCode)
    {
        ArgumentNullException.ThrowIfNull(uiCulture);
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return uiCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo($"{uiCulture.TwoLetterISOLanguageName}-{regionCode.Trim()}", predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return uiCulture;
        }
    }
}
