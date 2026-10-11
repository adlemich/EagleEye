using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Reports;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What the Reports page shows (US-004 AC-20).</summary>
public enum ReportsState
{
    /// <summary>Not paired or not connected: "No data available".</summary>
    NoData,

    /// <summary>Connected, no account under parental control.</summary>
    NoControlledAccounts,

    /// <summary>Fetching: "Loading …".</summary>
    Loading,

    /// <summary>The account selection and the days.</summary>
    Report,
}

/// <summary>An entry of the account selection: an account under parental control with its shown name (US-003 AC-11).</summary>
public sealed record AccountOption(string Sid, string DisplayName)
{
    /// <summary>The shown name (used by the account selection).</summary>
    public override string ToString() => DisplayName;
}

/// <summary>
/// The Reports page (US-004 AC-17 to AC-23): account selection with the controlled accounts (names and order as in
/// US-003), the first one selected on opening and when the selected one disappears; the days of the selected
/// account, today first, then older days with usage. Follows both models on the UI thread, never polls.
/// </summary>
public sealed class ReportsViewModel : ObservableObject
{
    private readonly IUserAccountsModel _accounts;
    private readonly IAccountUsageModel _usage;
    private readonly ControlledAccountSelection _selection = new();
    private AccountOption? _selectedAccount;
    private ReportsState _state = ReportsState.NoData;

    /// <summary>Creates the view model and follows the models.</summary>
    public ReportsViewModel(IUserAccountsModel accounts, IAccountUsageModel usage, IUiDispatcher dispatcher)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        ArgumentNullException.ThrowIfNull(dispatcher);
        Apply();
        accounts.Changed += () => dispatcher.Post(Apply);
        usage.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The controlled accounts (AC-17).</summary>
    public ObservableCollection<AccountOption> Accounts => _selection.Accounts;

    /// <summary>The days, today first (AC-18).</summary>
    public ObservableCollection<DayUsageViewModel> Days { get; } = [];

    /// <summary>The selected account; selecting fetches its usage (AC-21).</summary>
    public AccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                _usage.SelectAccount(value?.Sid);
            }
        }
    }

    /// <summary>What the page shows.</summary>
    public ReportsState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsReportVisible));
                OnPropertyChanged(nameof(IsAccountSelectionVisible));
            }
        }
    }

    /// <summary>The status text of the empty states (AC-20), empty with a report.</summary>
    public string StatusText => State switch
    {
        ReportsState.NoData => AppTexts.AccountsNoData,
        ReportsState.NoControlledAccounts => AppTexts.ReportsNoControlledAccounts,
        ReportsState.Loading => AppTexts.AccountsLoading,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => State != ReportsState.Report;

    /// <summary>The days are shown.</summary>
    public bool IsReportVisible => State == ReportsState.Report;

    /// <summary>The account selection is shown (with a report, and while the selected account is loading).</summary>
    public bool IsAccountSelectionVisible => State is ReportsState.Report or ReportsState.Loading && Accounts.Count > 0;

    private void Apply()
    {
        _selection.Update(_accounts, _selectedAccount, account => SelectedAccount = account);
        State = _accounts.LoadState switch
        {
            AccountsLoadState.NotAvailable => ReportsState.NoData,
            AccountsLoadState.Loading => ReportsState.Loading,
            _ when Accounts.Count == 0 => ReportsState.NoControlledAccounts,
            _ => _usage.LoadState switch
            {
                AccountsLoadState.Ready => ReportsState.Report,
                AccountsLoadState.Loading => ReportsState.Loading,
                _ => ReportsState.NoData,
            },
        };
        OnPropertyChanged(nameof(IsAccountSelectionVisible));
        MergeDays();
    }

    private void MergeDays()
    {
        if (State != ReportsState.Report)
        {
            Days.Clear();
            return;
        }

        var today = _usage.ServiceToday!.Value; // Ready always has a service date.
        var days = _usage.Days.ToDictionary(d => d.Day, d => d.Apps);
        days.TryAdd(today, []);
        var ordered = days.OrderByDescending(d => d.Key).ToList();
        // Days never change their order, so removing the gone ones and inserting the new ones keeps the list sorted.
        for (var index = Days.Count - 1; index >= 0; index--)
        {
            if (!days.ContainsKey(Days[index].Day))
            {
                Days.RemoveAt(index);
            }
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var (day, apps) = ordered[index];
            var model = FindDay(day);
            if (model is null)
            {
                model = new DayUsageViewModel(day, day == today);
                Days.Insert(index, model);
            }

            model.Update(apps, day == today);
        }
    }

    private DayUsageViewModel? FindDay(DateOnly day)
    {
        foreach (var model in Days)
        {
            if (model.Day == day)
            {
                return model;
            }
        }

        return null;
    }
}
