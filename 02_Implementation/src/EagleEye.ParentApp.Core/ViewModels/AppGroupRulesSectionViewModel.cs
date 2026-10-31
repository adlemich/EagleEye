using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Groups;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The section "App group rules" of the Rules page (US-008 AC-15 to AC-18), for the account selected by
/// <see cref="RulesViewModel"/>: one row per group (name and members from "AppGroups", rule from "AppGroupRules:{sid}"),
/// sorted by name in the parent's culture ignoring case, then by id; below the name the members, comma-separated
/// (OQ-4), or "(no apps)". Empty groups are listed; without groups "No app groups yet. …". The rows are the app rule rows
/// of US-006 (<see cref="AppRuleRowViewModel"/>), so editing, validation and messages are the same (AC-16). A change on the
/// App Groups page shows here through the "AppGroups" broadcast (AC-18). Follows the models on the UI thread.
/// </summary>
public sealed class AppGroupRulesSectionViewModel : ObservableObject, IAppRuleRowHost
{
    private readonly IAppGroupRulesModel _rules;
    private readonly IAppGroupsModel _groups;
    private readonly IUiDispatcher _dispatcher;
    private AccountsLoadState _state = AccountsLoadState.NotAvailable;
    private string _errorText = string.Empty;

    /// <summary>Creates the section and follows the models.</summary>
    public AppGroupRulesSectionViewModel(IAppGroupRulesModel rules, IAppGroupsModel groups, IUiDispatcher dispatcher)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Apply();
        rules.Changed += () => dispatcher.Post(Apply);
        groups.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>One row per group, sorted by name (AC-15).</summary>
    public ObservableCollection<AppRuleRowViewModel> Rows { get; } = [];

    /// <summary>Whether the group rules and the groups are available.</summary>
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

    /// <summary>The table is shown.</summary>
    public bool IsReady => State == AccountsLoadState.Ready;

    /// <summary>"Loading …" or "No data available", empty when ready.</summary>
    public string StatusText => State switch
    {
        AccountsLoadState.Loading => AppTexts.AccountsLoading,
        AccountsLoadState.NotAvailable => AppTexts.AccountsNoData,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => !IsReady;

    /// <summary>"No app groups yet. …" instead of the table (AC-15).</summary>
    public bool ShowNoGroups => IsReady && Rows.Count == 0;

    /// <summary>The table header and rows are shown.</summary>
    public bool HasRows => Rows.Count > 0;

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

    /// <summary>Follows the account selection of the page.</summary>
    public void SelectAccount(string? accountSid) => _rules.SelectAccount(accountSid);

    /// <summary>Commits typed budgets; called before the page or the account changes.</summary>
    public void FlushPendingEdits()
    {
        foreach (var row in Rows)
        {
            row.CommitHours();
            row.CommitMinutes();
        }
    }

    /// <inheritdoc />
    void IAppRuleRowHost.Post(Action action) => _dispatcher.Post(action);

    /// <inheritdoc />
    async Task<bool> IAppRuleRowHost.SendAsync(Func<IRuleTableWrites, Task<bool>> write)
    {
        var ok = await write(_rules);
        _dispatcher.Post(() =>
        {
            ErrorText = ok ? string.Empty : AppTexts.AccountSaveFailed;
            Apply();
        });
        return ok;
    }

    /// <inheritdoc />
    void IAppRuleRowHost.ShowProblem(AppBudgetProblem problem) =>
        ErrorText = problem == AppBudgetProblem.InvalidHours ? AppTexts.RulesInvalidHours : AppTexts.RulesInvalidBudgetMinutes;

    private void Apply()
    {
        State = LoadStates.Both(_rules.LoadState, _groups.LoadState);
        var rows = IsReady ? GroupRows(_groups.Snapshot!, _rules.Snapshot!) : []; // Ready always has both snapshots.
        OrderedMerge.Apply(Rows, rows, row => row.AppId, rule => rule.AppId, rule => new AppRuleRowViewModel(rule, this), (row, rule) => row.Update(rule));
        OnPropertyChanged(nameof(ShowNoGroups));
        OnPropertyChanged(nameof(HasRows));
    }

    /// <summary>The rows as app-rule records: id = group, name = group name, path = members line; no stored rule = 0:00, no day.</summary>
    private static List<AppRuleDto> GroupRows(AppGroupsDto groups, AppGroupRulesDto rules)
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        return [.. groups.Groups
            .OrderBy(g => g.Name, comparer)
            .ThenBy(g => g.GroupId)
            .Select(g =>
            {
                var rule = rules.Rules.FirstOrDefault(r => r.GroupId == g.GroupId);
                var members = groups.Apps.Where(a => a.GroupId == g.GroupId).Select(a => a.DisplayName).Order(comparer).ToList();
                var line = members.Count == 0 ? AppTexts.RulesGroupNoApps : string.Join(", ", members);
                return new AppRuleDto(g.GroupId, g.Name, line, rule?.BudgetMinutes ?? 0, rule?.Days ?? BreakTimeDays.None);
            })];
    }
}
