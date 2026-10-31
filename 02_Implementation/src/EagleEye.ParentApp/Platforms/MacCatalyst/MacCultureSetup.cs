using System.Globalization;
using EagleEye.ParentApp.Core.Localization;
using Foundation;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// Sets the UI language and formats from macOS before MAUI builds the UI (ADR-020 §4, US-007 AC-13, AC-15): the first
/// preferred language of the process (it reflects a per-app language) and the macOS region. Not unit-tested (platform
/// code); the rule is <see cref="UiCulturePolicy"/>.
/// </summary>
public static class MacCultureSetup
{
    /// <summary>Applies the cultures to the current and all future threads.</summary>
    public static void Apply()
    {
        var uiCulture = UiCulturePolicy.ResolveUiCulture(NSLocale.PreferredLanguages.FirstOrDefault());
        var formatting = UiCulturePolicy.ResolveFormattingCulture(uiCulture, NSLocale.CurrentLocale.CountryCode);
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = formatting;
        CultureInfo.DefaultThreadCurrentCulture = formatting;
    }
}
