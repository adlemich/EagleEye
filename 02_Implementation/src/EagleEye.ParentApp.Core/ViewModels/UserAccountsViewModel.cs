using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The section "User accounts on the EagleEye PC" (US-003 AC-6 to AC-17). Follows the model on the
/// UI thread: status text when there is no list, otherwise one row per standard account, sorted by
/// shown name (culture, ignoring case). Snapshots are merged by SID (rows are updated in place).
/// A row with a pending write keeps the requested value until the write completes; then it shows
/// the stored value, and an error text if the write failed (AC-16).
/// </summary>
public sealed class UserAccountsViewModel : ObservableObject
{
    private readonly IUserAccountsModel _model;
    private readonly IUiDispatcher _dispatcher;
    private AccountsSectionState _sectionState = AccountsSectionState.NoData;
    private AccountsLoadState? _lastLoadState;
    private string? _errorText;

    /// <summary>Creates the view model and follows the model.</summary>
    public UserAccountsViewModel(IUserAccountsModel model, IUiDispatcher dispatcher)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Apply();
        model.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The rows (only in <see cref="AccountsSectionState.List"/>).</summary>
    public ObservableCollection<UserAccountItemViewModel> Accounts { get; } = [];

    /// <summary>What the section shows.</summary>
    public AccountsSectionState SectionState
    {
        get => _sectionState;
        private set
        {
            if (SetProperty(ref _sectionState, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsListVisible));
            }
        }
    }

    /// <summary>The status text ("No data available", "Loading …", "No non-admin accounts available"), or empty with a list.</summary>
    public string StatusText => SectionState switch
    {
        AccountsSectionState.NoData => AppTexts.AccountsNoData,
        AccountsSectionState.Loading => AppTexts.AccountsLoading,
        AccountsSectionState.NoAccounts => AppTexts.AccountsNone,
        _ => string.Empty,
    };

    /// <summary>The status text is shown (no list).</summary>
    public bool HasStatus => SectionState != AccountsSectionState.List;

    /// <summary>The instruction and the rows are shown.</summary>
    public bool IsListVisible => SectionState == AccountsSectionState.List;

    /// <summary>The error of the last change (AC-16), or <c>null</c>.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Whether <see cref="ErrorText"/> is shown.</summary>
    public bool HasError => ErrorText is not null;

    /// <summary>The last write started by a checkbox (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    private void Apply()
    {
        var loadState = _model.LoadState;
        var snapshot = _model.Snapshot;
        if (loadState != _lastLoadState && loadState != AccountsLoadState.Ready)
        {
            // Connected (Loading) or disconnected (NotAvailable): an old error no longer applies.
            ErrorText = null;
        }

        _lastLoadState = loadState;
        if (loadState != AccountsLoadState.Ready || snapshot is null)
        {
            Accounts.Clear();
            SectionState = loadState == AccountsLoadState.Loading ? AccountsSectionState.Loading : AccountsSectionState.NoData;
            return;
        }

        Merge(snapshot);
        SectionState = Accounts.Count == 0 ? AccountsSectionState.NoAccounts : AccountsSectionState.List;
    }

    private void Merge(UserAccountListDto snapshot)
    {
        var comparer = AccountDisplayName.Comparer;
        var ordered = snapshot.Accounts
            .Select(account => (Account: account, Name: AccountDisplayName.Format(account)))
            .OrderBy(entry => entry.Name, comparer)
            .ToList();
        var sids = ordered.Select(entry => entry.Account.Sid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = Accounts.Count - 1; index >= 0; index--)
        {
            if (!sids.Contains(Accounts[index].Sid))
            {
                Accounts.RemoveAt(index);
            }
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var (account, name) = ordered[index];
            var item = Find(account.Sid);
            if (item is null)
            {
                Accounts.Insert(index, new UserAccountItemViewModel(account.Sid, name, account.IsUnderParentalControl, OnToggled));
                continue;
            }

            item.DisplayName = name;
            if (!item.IsPending)
            {
                item.ApplyStoredValue(account.IsUnderParentalControl);
            }

            var current = Accounts.IndexOf(item);
            if (current != index)
            {
                Accounts.Move(current, index);
            }
        }
    }

    private UserAccountItemViewModel? Find(string sid)
    {
        return Accounts.FirstOrDefault(item => string.Equals(item.Sid, sid, StringComparison.OrdinalIgnoreCase));
    }

    private void OnToggled(UserAccountItemViewModel item, bool value)
    {
        item.IsPending = true;
        item.IsEnabled = false;
        LastWrite = WriteAsync(item, value);
    }

    private async Task WriteAsync(UserAccountItemViewModel item, bool value)
    {
        var saved = await _model.SetParentalControlAsync(item.Sid, value).ConfigureAwait(false);
        _dispatcher.Post(() =>
        {
            ErrorText = saved ? null : AppTexts.AccountSaveFailed;
            item.IsPending = false;
            item.IsEnabled = true;
            if (FindStored(_model.Snapshot, item.Sid) is { } stored)
            {
                item.ApplyStoredValue(stored.IsUnderParentalControl);
            }
        });
    }

    private static UserAccountDto? FindStored(UserAccountListDto? snapshot, string sid)
    {
        foreach (var account in snapshot?.Accounts ?? [])
        {
            if (string.Equals(account.Sid, sid, StringComparison.OrdinalIgnoreCase))
            {
                return account;
            }
        }

        return null;
    }
}
