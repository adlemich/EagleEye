using System.Globalization;

namespace EagleEye.Service.UserAccounts;

/// <summary>
/// Decides which local accounts belong to the inventory (US-003 AC-1 to AC-3, FR-SVC-070): standard
/// accounts only, never admin accounts, never the built-in accounts (by relative ID, so renamed or
/// localized built-ins are caught), never the Windows setup leftover <c>defaultuser0</c> (by name,
/// ignoring case: Windows gives it no special SID or flag).
/// </summary>
public static class AccountInventoryFilter
{
    /// <summary>User name of the Windows setup leftover account (Michael, 2026-10-07, Q-4).</summary>
    public const string SetupLeftoverUserName = "defaultuser0";

    // Administrator, Guest, DefaultAccount, WDAGUtilityAccount.
    private static readonly HashSet<uint> BuiltInRelativeIds = [500, 501, 503, 504];

    /// <summary>Whether the SID's relative ID (last component) is one of the built-in accounts.</summary>
    public static bool IsBuiltIn(string sid)
    {
        ArgumentNullException.ThrowIfNull(sid);
        var separator = sid.LastIndexOf('-');
        return separator >= 0
            && uint.TryParse(sid.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var relativeId)
            && BuiltInRelativeIds.Contains(relativeId);
    }

    /// <summary>Whether the user name is the setup leftover <c>defaultuser0</c> (any casing).</summary>
    public static bool IsSetupLeftover(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return string.Equals(userName, SetupLeftoverUserName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the standard accounts: not built-in, not the setup leftover, not admin.</summary>
    public static IReadOnlyList<LocalAccountInfo> Standard(IEnumerable<LocalAccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return accounts.Where(a => !a.IsAdmin && !IsBuiltIn(a.Sid) && !IsSetupLeftover(a.UserName)).ToList();
    }
}
