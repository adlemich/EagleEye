using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The section "Application rules" of the Rules page (US-006 AC-1 to AC-12, AC-17), for the account selected by
/// <see cref="RulesViewModel"/>: the table of app records (rows merged by app id, sorted by display name in the parent's
/// culture ignoring case, then by id; a new record is inserted at its place, AC-4), "No apps recorded yet." when empty,
/// the budget text (saved when the parent leaves the box and before the page or the account changes; ADR-016 for the
/// default) and one message line. Follows the model on the UI thread; never polls.
/// </summary>
public sealed class AppRulesSectionViewModel : ObservableObject, IAppRuleRowHost
{
    private readonly IAppRulesModel _model;
    private readonly IUiDispatcher _dispatcher;
    private AccountsLoadState _state = AccountsLoadState.NotAvailable;
    private string _budgetText = string.Empty;
    private bool _budgetTextDirty;
    private string _errorText = string.Empty;

    /// <summary>Creates the section and follows the model.</summary>
    public AppRulesSectionViewModel(IAppRulesModel model, IUiDispatcher dispatcher)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Apply();
        model.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The rows of the selected account, sorted by display name (AC-2).</summary>
    public ObservableCollection<AppRuleRowViewModel> Rows { get; } = [];

    /// <summary>Whether the app rules of the selected account are available.</summary>
    public AccountsLoadState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>The table and the budget text are shown.</summary>
    public bool IsReady => State == AccountsLoadState.Ready;

    /// <summary>"Loading …" or "No data available" (AC-6), empty when ready.</summary>
    public string StatusText => State switch
    {
        AccountsLoadState.Loading => AppTexts.AccountsLoading,
        AccountsLoadState.NotAvailable => AppTexts.AccountsNoData,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => !IsReady;

    /// <summary>"No apps recorded yet." instead of rows (AC-3).</summary>
    public bool ShowNoApps => IsReady && Rows.Count == 0;

    /// <summary>Maximum length of the budget text (AC-10).</summary>
    public int BudgetTextMaxLength => BreakTimeRules.MaxDisplayTextLength;

    /// <summary>The budget text as typed or confirmed (AC-10).</summary>
    public string BudgetText
    {
        get => _budgetText;
        set
        {
            if (SetProperty(ref _budgetText, value ?? string.Empty))
            {
                // WinUI returns "\r" line breaks; only a real change counts as typing.
                _budgetTextDirty = BreakTimeRules.NormalizeLineBreaks(_budgetText) != ConfirmedText;
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

    /// <summary>The last write started by the section (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    private string ConfirmedText =>
        _model.Snapshot is { } snapshot
            ? KidTextEditing.ShownText(snapshot.BudgetText, DefaultTextKind.BudgetText, CultureInfo.CurrentUICulture)
            : string.Empty;

    /// <summary>Follows the account selection of the page (AC-1).</summary>
    public void SelectAccount(string? accountSid) => _model.SelectAccount(accountSid);

    /// <summary>Saves the budget text when the parent leaves the box (AC-10); empty or a default restores the default.</summary>
    public void CommitBudgetText()
    {
        if (!_budgetTextDirty)
        {
            return;
        }

        _budgetTextDirty = false;
        var confirmed = ConfirmedText;
        var send = KidTextEditing.Commit(_budgetText, confirmed, _model.Snapshot?.BudgetText is null, DefaultTextKind.BudgetText);
        if (send is null)
        {
            SetProperty(ref _budgetText, confirmed, nameof(BudgetText));
            return;
        }

        LastWrite = SendAsync(model => model.SetBudgetTextAsync(send));
    }

    /// <summary>Commits typed budgets and the budget text; called before the page or the account changes.</summary>
    public void FlushPendingEdits()
    {
        foreach (var row in Rows)
        {
            row.CommitHours();
            row.CommitMinutes();
        }

        CommitBudgetText();
    }

    /// <inheritdoc />
    void IAppRuleRowHost.Post(Action action) => _dispatcher.Post(action);

    /// <inheritdoc />
    Task<bool> IAppRuleRowHost.SendAsync(Func<IAppRulesModel, Task<bool>> write) => SendAsync(write);

    /// <inheritdoc />
    void IAppRuleRowHost.ShowProblem(AppBudgetProblem problem) =>
        ErrorText = problem == AppBudgetProblem.InvalidHours ? AppTexts.RulesInvalidHours : AppTexts.RulesInvalidBudgetMinutes;

    private async Task<bool> SendAsync(Func<IAppRulesModel, Task<bool>> write)
    {
        var ok = await write(_model);
        _dispatcher.Post(() =>
        {
            ErrorText = ok ? string.Empty : AppTexts.AccountSaveFailed;
            Apply();
        });
        return ok;
    }

    private void Apply()
    {
        State = _model.LoadState;
        MergeRows();
        if (!_budgetTextDirty)
        {
            SetProperty(ref _budgetText, IsReady ? ConfirmedText : string.Empty, nameof(BudgetText));
        }

        OnPropertyChanged(nameof(ShowNoApps));
    }

    private void MergeRows()
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var apps = IsReady
            ? [.. _model.Snapshot!.Apps.OrderBy(a => a.DisplayName, comparer).ThenBy(a => a.AppId)] // Ready always has a snapshot.
            : new List<AppRuleDto>();
        var ids = apps.Select(a => a.AppId).ToHashSet();
        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (!ids.Contains(Rows[index].AppId))
            {
                Rows.RemoveAt(index);
            }
        }

        // Existing rows are updated and moved to their sorted place; new ones are inserted there (AC-4).
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            var current = FindRow(app.AppId);
            if (current < 0)
            {
                Rows.Insert(index, new AppRuleRowViewModel(app, this));
                continue;
            }

            Rows[current].Update(app);
            if (current != index)
            {
                Rows.Move(current, index);
            }
        }
    }

    private int FindRow(long appId)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            if (Rows[index].AppId == appId)
            {
                return index;
            }
        }

        return -1;
    }
}
