using System.Collections.ObjectModel;
using EagleEye.ParentApp.Core.Accounts;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The account selection of the Reports and Rules pages (US-004 AC-17, US-005 AC-2): the accounts under parental
/// control with their shown names, sorted as in US-003; the selected account is kept while it is listed, otherwise
/// the first one is selected. Merged in place so that the picker keeps its entries. UI thread only.
/// </summary>
/// <remarks>
/// US-005 smoke check: the WinUI ComboBox behind MAUI's <c>Picker</c> fails fast (E_BOUNDS) when its selected item is
/// removed while it is not the first one (e.g. all accounts disappear on a lost connection). <see cref="Update"/>
/// therefore clears the selection before the selected entry is removed.
/// </remarks>
public sealed class ControlledAccountSelection
{
    /// <summary>The controlled accounts.</summary>
    public ObservableCollection<AccountOption> Accounts { get; } = [];

    /// <summary>
    /// Updates <see cref="Accounts"/> from the account model (empty unless its list is ready) and returns the account
    /// to select. <paramref name="select"/> is called with <c>null</c> before a selected entry is removed, and finally
    /// with the account to select (the current one if still listed, otherwise the first one, or none).
    /// </summary>
    public void Update(IUserAccountsModel accounts, AccountOption? current, Action<AccountOption?> select)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(select);
        var snapshot = accounts.LoadState == AccountsLoadState.Ready ? accounts.Snapshot : null;
        var desired = (snapshot?.Accounts ?? [])
            .Where(a => a.IsUnderParentalControl)
            .Select(a => new AccountOption(a.Sid, AccountDisplayName.Format(a)))
            .OrderBy(a => a.DisplayName, AccountDisplayName.Comparer)
            .ToList();
        if (current is not null && !desired.Contains(current))
        {
            select(null);
        }

        for (var index = Accounts.Count - 1; index >= 0; index--)
        {
            if (!desired.Contains(Accounts[index]))
            {
                Accounts.RemoveAt(index);
            }
        }

        // Unchanged entries keep their relative order (a renamed account is a new entry), so inserting the new
        // entries at their positions gives the sorted list.
        for (var index = 0; index < desired.Count; index++)
        {
            if (!Accounts.Contains(desired[index]))
            {
                Accounts.Insert(index, desired[index]);
            }
        }

        select(current is not null && Accounts.FirstOrDefault(a => a.Sid == current.Sid) is { } still ? still : Accounts.FirstOrDefault());
    }
}
