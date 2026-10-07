using System.Globalization;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The shown name of an account (US-003 AC-11, AC-12): "Max Adler (max)", or "max" without a full
/// name, followed by "(disabled)" / "(deaktiviert)" for disabled accounts. Sorted with the current
/// culture, ignoring case (AC-10).
/// </summary>
public static class AccountDisplayName
{
    /// <summary>Compares shown names: current culture, ignoring case.</summary>
    public static StringComparer Comparer => StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);

    /// <summary>Returns the shown name.</summary>
    public static string Format(UserAccountDto account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var name = string.IsNullOrWhiteSpace(account.FullName)
            ? account.UserName
            : AppTexts.Format(AppTexts.AccountNameFormat, account.FullName, account.UserName);
        return account.IsDisabled ? name + " " + AppTexts.AccountDisabledSuffix : name;
    }
}
