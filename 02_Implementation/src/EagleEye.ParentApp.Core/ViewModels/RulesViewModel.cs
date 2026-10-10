using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What the Rules page shows (US-005 AC-6).</summary>
public enum RulesState
{
    /// <summary>Not paired or not connected: "No data available".</summary>
    NoData,

    /// <summary>Connected, no account under parental control.</summary>
    NoControlledAccounts,

    /// <summary>Fetching: "Loading …".</summary>
    Loading,

    /// <summary>The account selection, the table and the display text.</summary>
    Rules,
}

/// <summary>
/// The Rules page (US-005 AC-1 to AC-20): account selection as on Reports, the break-time table (rows merged by entry
/// id, creation order), "Add new entry" (disabled at 20 entries), the display text (saved when the parent leaves the
/// box, and before the page or the account changes, <see cref="FlushPendingEdits"/>) and one message line for refused
/// edits and failed writes. Follows both models on the UI thread; never polls.
/// </summary>
public sealed class RulesViewModel : ObservableObject, IBreakTimeRowHost
{
    private readonly IUserAccountsModel _accounts;
    private readonly IAccountRulesModel _rules;
    private readonly IUiDispatcher _dispatcher;
    private readonly ControlledAccountSelection _selection = new();
    private AccountOption? _selectedAccount;
    private RulesState _state = RulesState.NoData;
    private string _displayText = string.Empty;
    private bool _displayTextDirty;
    private string _errorText = string.Empty;

    /// <summary>Creates the view model and follows the models.</summary>
    public RulesViewModel(IUserAccountsModel accounts, IAccountRulesModel rules, IUiDispatcher dispatcher)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        AddEntryCommand = new RelayCommand(() => LastWrite = SendAsync(model => model.AddEntryAsync()), () => CanAddEntry);
        Apply();
        accounts.Changed += () => dispatcher.Post(Apply);
        rules.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The controlled accounts (AC-2).</summary>
    public ObservableCollection<AccountOption> Accounts => _selection.Accounts;

    /// <summary>The rows of the selected account, in creation order (AC-3).</summary>
    public ObservableCollection<BreakTimeRowViewModel> Rows { get; } = [];

    /// <summary>"Add new entry" (AC-7).</summary>
    public IRelayCommand AddEntryCommand { get; }

    /// <summary>Maximum length of the display text in the edit box (OQ-8, D-8).</summary>
    public int DisplayTextMaxLength => BreakTimeRules.MaxDisplayTextLength;

    /// <summary>The selected account; pending edits are saved first, then its rules are fetched (AC-5, AC-15).</summary>
    public AccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (Equals(_selectedAccount, value))
            {
                return;
            }

            FlushPendingEdits();
            _selectedAccount = value;
            OnPropertyChanged();
            _rules.SelectAccount(value?.Sid);
        }
    }

    /// <summary>What the page shows.</summary>
    public RulesState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsRulesVisible));
            }
        }
    }

    /// <summary>The status text of the empty states (AC-6), empty with rules.</summary>
    public string StatusText => State switch
    {
        RulesState.NoData => AppTexts.AccountsNoData,
        RulesState.NoControlledAccounts => AppTexts.ReportsNoControlledAccounts,
        RulesState.Loading => AppTexts.AccountsLoading,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => State != RulesState.Rules;

    /// <summary>The table and the display text are shown.</summary>
    public bool IsRulesVisible => State == RulesState.Rules;

    /// <summary>The account selection is shown (with rules, and while the selected account is loading).</summary>
    public bool IsAccountSelectionVisible => State is RulesState.Rules or RulesState.Loading && Accounts.Count > 0;

    /// <summary>"No break times defined." instead of rows (AC-3).</summary>
    public bool ShowNoEntries => State == RulesState.Rules && Rows.Count == 0;

    /// <summary>Whether another entry may be added (OQ-8).</summary>
    public bool CanAddEntry => State == RulesState.Rules && BreakTimeEditRules.CanAdd(Rows.Count);

    /// <summary>The display text as typed or confirmed (AC-15, AC-16).</summary>
    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (SetProperty(ref _displayText, value ?? string.Empty))
            {
                // WinUI returns "\r" line breaks; only a real change counts as typing (plan TI-12 f).
                _displayTextDirty = BreakTimeRules.NormalizeLineBreaks(_displayText) != ConfirmedText;
            }
        }
    }

    /// <summary>The message of the last refused edit or failed write; cleared by the next successful change.</summary>
    public string ErrorText
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

    /// <summary>Whether a message is shown.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The last write started by the page (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    private string ConfirmedText => _rules.Snapshot?.DisplayText ?? string.Empty;

    /// <summary>Saves the display text when the parent leaves the box (AC-15); empty restores the default (OQ-8).</summary>
    public void CommitDisplayText()
    {
        if (!_displayTextDirty)
        {
            return;
        }

        _displayTextDirty = false;
        var text = BreakTimeRules.NormalizeLineBreaks(_displayText);
        if (text != ConfirmedText)
        {
            LastWrite = SendAsync(model => model.SetDisplayTextAsync(text));
        }
    }

    /// <summary>Commits typed times and the display text; called before the page or the account changes (AC-15).</summary>
    public void FlushPendingEdits()
    {
        foreach (var row in Rows)
        {
            row.CommitStart();
            row.CommitEnd();
        }

        CommitDisplayText();
    }

    /// <inheritdoc />
    void IBreakTimeRowHost.Post(Action action) => _dispatcher.Post(action);

    /// <inheritdoc />
    Task<bool> IBreakTimeRowHost.SendAsync(Func<IAccountRulesModel, Task<bool>> write) => SendAsync(write);

    /// <inheritdoc />
    void IBreakTimeRowHost.ShowProblem(EditProblem problem)
    {
        ErrorText = problem switch
        {
            EditProblem.InvalidTime => AppTexts.RulesInvalidTime,
            EditProblem.EndNotAfterStart => AppTexts.RulesEndBeforeStart,
            _ => AppTexts.RulesNoDaySelected,
        };
    }

    private async Task<bool> SendAsync(Func<IAccountRulesModel, Task<bool>> write)
    {
        var ok = await write(_rules).ConfigureAwait(false);
        _dispatcher.Post(() =>
        {
            ErrorText = ok ? string.Empty : AppTexts.AccountSaveFailed;
            Apply();
        });
        return ok;
    }

    private void Apply()
    {
        _selection.Merge(_accounts);
        SelectedAccount = _selection.Choose(_selectedAccount);
        State = _accounts.LoadState switch
        {
            AccountsLoadState.NotAvailable => RulesState.NoData,
            AccountsLoadState.Loading => RulesState.Loading,
            _ when Accounts.Count == 0 => RulesState.NoControlledAccounts,
            _ => _rules.LoadState switch
            {
                AccountsLoadState.Ready => RulesState.Rules,
                AccountsLoadState.Loading => RulesState.Loading,
                _ => RulesState.NoData,
            },
        };
        MergeRows();
        if (!_displayTextDirty)
        {
            SetProperty(ref _displayText, State == RulesState.Rules ? ConfirmedText : string.Empty, nameof(DisplayText));
        }

        OnPropertyChanged(nameof(IsAccountSelectionVisible));
        OnPropertyChanged(nameof(ShowNoEntries));
        OnPropertyChanged(nameof(CanAddEntry));
        AddEntryCommand.NotifyCanExecuteChanged();
    }

    private void MergeRows()
    {
        IReadOnlyList<BreakTimeEntryDto> entries = State == RulesState.Rules ? _rules.Snapshot!.BreakTimes : []; // Ready always has a snapshot.
        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (!entries.Any(e => e.EntryId == Rows[index].EntryId))
            {
                Rows.RemoveAt(index);
            }
        }

        // Entries keep their creation order, so inserting the new ones at their positions keeps the rows in order.
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (Rows.FirstOrDefault(r => r.EntryId == entry.EntryId) is { } row)
            {
                row.Update(entry);
            }
            else
            {
                Rows.Insert(index, new BreakTimeRowViewModel(entry, this));
            }
        }
    }
}
