using System.Collections.ObjectModel;
using EagleEye.ParentApp.Core.Accounts;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The account selection of the Reports and Rules pages (US-004 AC-17, US-005 AC-2): the accounts under parental
/// control with their shown names, sorted as in US-003; the selected account is kept while it is listed, otherwise
/// the first one is selected. Merged in place so that the picker keeps its entries. UI thread only.
/// </summary>
public sealed class ControlledAccountSelection
{
    /// <summary>The controlled accounts.</summary>
    public ObservableCollection<AccountOption> Accounts { get; } = [];

    /// <summary>Updates <see cref="Accounts"/> from the account model (empty unless its list is ready).</summary>
    public void Merge(IUserAccountsModel accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var snapshot = accounts.LoadState == AccountsLoadState.Ready ? accounts.Snapshot : null;
        var desired = (snapshot?.Accounts ?? [])
            .Where(a => a.IsUnderParentalControl)
            .Select(a => new AccountOption(a.Sid, AccountDisplayName.Format(a)))
            .OrderBy(a => a.DisplayName, AccountDisplayName.Comparer)
            .ToList();
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
    }

    /// <summary>The account to select: the current one if it is still listed, otherwise the first one (or none).</summary>
    public AccountOption? Choose(AccountOption? current) =>
        current is not null && Accounts.FirstOrDefault(a => a.Sid == current.Sid) is { } still ? still : Accounts.FirstOrDefault();
}
