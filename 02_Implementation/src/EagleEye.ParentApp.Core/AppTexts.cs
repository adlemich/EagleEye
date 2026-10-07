using System.Globalization;
using System.Resources;

namespace EagleEye.ParentApp.Core;

/// <summary>
/// Localized user-facing texts of the parent app (NFR-L-010 to NFR-L-012). German is the
/// neutral/default language (<c>Resources/AppTexts.resx</c>); English is in
/// <c>Resources/AppTexts.en.resx</c>. The current UI culture selects the language, and any
/// language other than English falls back to German.
/// </summary>
public static class AppTexts
{
    /// <summary>Resource manager for the texts (exposed for completeness tests).</summary>
    internal static readonly ResourceManager ResourceManager =
        new("EagleEye.ParentApp.Core.Resources.AppTexts", typeof(AppTexts).Assembly);

#pragma warning disable CS1591 // The property names are the resource keys; see the resx files for the texts.
    public static string MenuSettings => Get(nameof(MenuSettings));
    public static string SectionAppearance => Get(nameof(SectionAppearance));
    public static string ThemeLight => Get(nameof(ThemeLight));
    public static string ThemeDark => Get(nameof(ThemeDark));
    public static string SectionServerConnection => Get(nameof(SectionServerConnection));
    public static string PairingStatusLabel => Get(nameof(PairingStatusLabel));
    public static string PairingStatusNotPaired => Get(nameof(PairingStatusNotPaired));
    public static string PairingStatusInProgress => Get(nameof(PairingStatusInProgress));
    public static string PairingStatusPaired => Get(nameof(PairingStatusPaired));
    public static string HostLabel => Get(nameof(HostLabel));
    public static string HostPlaceholder => Get(nameof(HostPlaceholder));
    public static string HostDialogTitle => Get(nameof(HostDialogTitle));
    public static string ConnectButton => Get(nameof(ConnectButton));
    public static string PairingInstruction => Get(nameof(PairingInstruction));
    public static string PairingCodeLabel => Get(nameof(PairingCodeLabel));
    public static string DeviceNameLabel => Get(nameof(DeviceNameLabel));
    public static string PairButton => Get(nameof(PairButton));
    public static string NewCodeButton => Get(nameof(NewCodeButton));
    public static string CancelButton => Get(nameof(CancelButton));
    public static string PairedWithFormat => Get(nameof(PairedWithFormat));
    public static string ThisDeviceFormat => Get(nameof(ThisDeviceFormat));
    public static string RemovePairingButton => Get(nameof(RemovePairingButton));
    public static string RemoveConfirmTitle => Get(nameof(RemoveConfirmTitle));
    public static string RemoveConfirmMessageFormat => Get(nameof(RemoveConfirmMessageFormat));
    public static string RemoveNeedsConnection => Get(nameof(RemoveNeedsConnection));
    public static string StatusConnectedFormat => Get(nameof(StatusConnectedFormat));
    public static string StatusConnectingFormat => Get(nameof(StatusConnectingFormat));
    public static string StatusNotConnected => Get(nameof(StatusNotConnected));
    public static string StatusNotConnectedFormat => Get(nameof(StatusNotConnectedFormat));
    public static string ErrorUnreachableFormat => Get(nameof(ErrorUnreachableFormat));
    public static string ErrorInvalidHost => Get(nameof(ErrorInvalidHost));
    public static string ErrorCodeFormat => Get(nameof(ErrorCodeFormat));
    public static string ErrorWrongCode => Get(nameof(ErrorWrongCode));
    public static string ErrorCodeExpired => Get(nameof(ErrorCodeExpired));
    public static string ErrorNoPendingCode => Get(nameof(ErrorNoPendingCode));
    public static string ErrorDeviceNameRequired => Get(nameof(ErrorDeviceNameRequired));
    public static string ErrorCertificateChangedFormat => Get(nameof(ErrorCertificateChangedFormat));
    public static string ErrorRemoveFailed => Get(nameof(ErrorRemoveFailed));
    public static string InfoPairingLostFormat => Get(nameof(InfoPairingLostFormat));
    public static string ErrorStartup => Get(nameof(ErrorStartup));
    public static string OkButton => Get(nameof(OkButton));
    public static string SectionUserAccounts => Get(nameof(SectionUserAccounts));
    public static string UserAccountsInstruction => Get(nameof(UserAccountsInstruction));
    public static string UnderParentalControl => Get(nameof(UnderParentalControl));
    public static string AccountsNoData => Get(nameof(AccountsNoData));
    public static string AccountsNone => Get(nameof(AccountsNone));
    public static string AccountsLoading => Get(nameof(AccountsLoading));
    public static string AccountDisabledSuffix => Get(nameof(AccountDisabledSuffix));
    public static string AccountNameFormat => Get(nameof(AccountNameFormat));
    public static string AccountSaveFailed => Get(nameof(AccountSaveFailed));
#pragma warning restore CS1591

    /// <summary>Formats a composite text with the current culture.</summary>
    public static string Format(string format, object? argument)
    {
        return string.Format(CultureInfo.CurrentCulture, format, argument);
    }

    /// <summary>Formats a composite text with two arguments and the current culture.</summary>
    public static string Format(string format, object? argument0, object? argument1)
    {
        return string.Format(CultureInfo.CurrentCulture, format, argument0, argument1);
    }

    /// <summary>Returns the text for the current UI culture.</summary>
    /// <exception cref="InvalidOperationException">The resource key does not exist.</exception>
    internal static string Get(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Missing parent app text resource '{name}'.");
    }
}
