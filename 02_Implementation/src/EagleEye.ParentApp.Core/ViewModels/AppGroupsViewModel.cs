using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Groups;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What the App Groups page shows (US-008 AC-5, the states of US-005 AC-6).</summary>
public enum AppGroupsState
{
    /// <summary>Not paired, not connected or the fetch failed: "No data available".</summary>
    NoData,

    /// <summary>Connected, no account under parental control.</summary>
    NoControlledAccounts,

    /// <summary>Fetching: "Loading …".</summary>
    Loading,

    /// <summary>The explanation, "Add group" and the sections.</summary>
    Ready,
}

/// <summary>
/// The App Groups page (US-008 AC-1 to AC-14): one section per group, sorted by name in the parent's culture ignoring case
/// (then by id), and "Not in any group" last; sections and entries are merged in place (AC-4). Creating, renaming,
/// deleting and moving are sent at once (AC-14); the name dialog opens again with a message while the name is invalid
/// (AC-8, plan D-5). Moving works by drag-and-drop (<see cref="BeginDrag"/> … <see cref="EndDrag"/>, AC-11) and by
/// "Move to …" (AC-12). No account selection: groups apply to all controlled accounts. Follows the models on the UI
/// thread; never polls.
/// </summary>
public sealed class AppGroupsViewModel : ObservableObject
{
    private readonly IUserAccountsModel _accounts;
    private readonly IAppGroupsModel _groups;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private AppGroupsState _state = AppGroupsState.NoData;
    private string _errorText = string.Empty;

    /// <summary>Creates the view model and follows the models.</summary>
    public AppGroupsViewModel(IUserAccountsModel accounts, IAppGroupsModel groups, IDialogService dialogs, IUiDispatcher dispatcher)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        AddGroupCommand = new AsyncRelayCommand(AddGroupAsync);
        Apply();
        accounts.Changed += () => dispatcher.Post(Apply);
        groups.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The groups, then "Not in any group" (AC-2).</summary>
    public ObservableCollection<AppGroupSectionViewModel> Sections { get; } = [];

    /// <summary>"Add group" (AC-7).</summary>
    public IAsyncRelayCommand AddGroupCommand { get; }

    /// <summary>What the page shows.</summary>
    public AppGroupsState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(IsReady));
            }
        }
    }

    /// <summary>The status text of the empty states (AC-5), empty when ready.</summary>
    public string StatusText => State switch
    {
        AppGroupsState.NoData => AppTexts.AccountsNoData,
        AppGroupsState.NoControlledAccounts => AppTexts.ReportsNoControlledAccounts,
        AppGroupsState.Loading => AppTexts.AccountsLoading,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => State != AppGroupsState.Ready;

    /// <summary>The explanation, "Add group" and the sections are shown.</summary>
    public bool IsReady => State == AppGroupsState.Ready;

    /// <summary>"The change could not be saved. Please try again." after a failed write (AC-14); cleared by the next success.</summary>
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

    /// <summary>The app being dragged (passed in-app, spike Step 1), or null.</summary>
    public GroupableAppViewModel? DraggedApp { get; private set; }

    /// <summary>The sections an app can be moved to: every section except its own (AC-12).</summary>
    public IReadOnlyList<AppGroupSectionViewModel> MoveTargets(GroupableAppViewModel app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return [.. Sections.Where(s => !ReferenceEquals(s, app.Section))];
    }

    /// <summary>Moves an app to a section; its own section changes nothing (AC-11, AC-13).</summary>
    public Task MoveAsync(GroupableAppViewModel app, AppGroupSectionViewModel target)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(target);
        return ReferenceEquals(app.Section, target) ? Task.CompletedTask : SendAsync(model => model.MoveAsync(app.ProgramPath, target.GroupId));
    }

    /// <summary>A drag of <paramref name="app"/> started.</summary>
    public void BeginDrag(GroupableAppViewModel app) => DraggedApp = app;

    /// <summary>The pointer is over <paramref name="section"/>: only that section is highlighted.</summary>
    public void DragOver(AppGroupSectionViewModel section)
    {
        foreach (var each in Sections)
        {
            each.IsDropTarget = ReferenceEquals(each, section);
        }
    }

    /// <summary>The pointer left <paramref name="section"/>.</summary>
    public void DragLeave(AppGroupSectionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);
        section.IsDropTarget = false;
    }

    /// <summary>The dragged app was dropped on <paramref name="section"/>: the highlight clears and the app moves.</summary>
    public Task DropAsync(AppGroupSectionViewModel section)
    {
        var app = DraggedApp;
        EndDrag();
        return app is null ? Task.CompletedTask : MoveAsync(app, section);
    }

    /// <summary>The drag ended (also outside every section): nothing is dragged or highlighted any more.</summary>
    public void EndDrag()
    {
        DraggedApp = null;
        foreach (var section in Sections)
        {
            section.IsDropTarget = false;
        }
    }

    /// <summary>"Rename": the name dialog with the current name; the unchanged name is valid (AC-9).</summary>
    internal async Task RenameAsync(AppGroupSectionViewModel section)
    {
        if (section.GroupId is not { } groupId || await PromptNameAsync(AppTexts.GroupsRenameTitle, section.Title, groupId) is not { } name)
        {
            return;
        }

        await SendAsync(model => model.RenameAsync(groupId, name));
    }

    /// <summary>"Delete" after the confirmation (AC-10).</summary>
    internal async Task DeleteAsync(AppGroupSectionViewModel section)
    {
        var message = AppTexts.Format(AppTexts.GroupsDeleteConfirmFormat, section.Title);
        if (section.GroupId is not { } groupId
            || !await _dialogs.ConfirmAsync(AppTexts.GroupsDeleteTitle, message, AppTexts.GroupsDelete, AppTexts.CancelButton))
        {
            return;
        }

        await SendAsync(model => model.DeleteAsync(groupId));
    }

    /// <summary>"Move to …": the action sheet with the targets (AC-12).</summary>
    internal async Task ChooseTargetAsync(GroupableAppViewModel app)
    {
        var targets = MoveTargets(app);
        if (await _dialogs.ChooseAsync(AppTexts.GroupsMoveTo, AppTexts.CancelButton, [.. targets.Select(t => t.Title)]) is { } index)
        {
            await MoveAsync(app, targets[index]);
        }
    }

    private async Task AddGroupAsync()
    {
        if (await PromptNameAsync(AppTexts.GroupsNewTitle, null, null) is { } name)
        {
            await SendAsync(model => model.CreateAsync(name));
        }
    }

    private async Task<string?> PromptNameAsync(string title, string? text, long? renamedGroupId)
    {
        string? problem = null;
        while (true)
        {
            var message = problem is null ? AppTexts.GroupsNameLabel : $"{problem}\n\n{AppTexts.GroupsNameLabel}";
            text = await _dialogs.PromptTextAsync(title, message, AppTexts.OkButton, AppTexts.CancelButton, text, AppGroupNames.MaxCodeUnits);
            if (text is null)
            {
                return null;
            }

            problem = AppGroupNameRules.Problem(text, _groups.Snapshot?.Groups ?? [], renamedGroupId);
            if (problem is null)
            {
                return AppGroupNames.Normalize(text);
            }
        }
    }

    private async Task SendAsync(Func<IAppGroupsModel, Task<bool>> write)
    {
        var ok = await write(_groups);
        _dispatcher.Post(() =>
        {
            ErrorText = ok ? string.Empty : AppTexts.AccountSaveFailed;
            Apply();
        });
    }

    private void Apply()
    {
        State = _accounts.LoadState switch
        {
            AccountsLoadState.NotAvailable => AppGroupsState.NoData,
            AccountsLoadState.Loading => AppGroupsState.Loading,
            _ when _accounts.Snapshot?.Accounts.Any(a => a.IsUnderParentalControl) != true => AppGroupsState.NoControlledAccounts,
            _ => _groups.LoadState switch
            {
                AccountsLoadState.Ready => AppGroupsState.Ready,
                AccountsLoadState.Loading => AppGroupsState.Loading,
                _ => AppGroupsState.NoData,
            },
        };
        MergeSections(IsReady ? _groups.Snapshot : null);
    }

    private void MergeSections(AppGroupsDto? snapshot)
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var groups = (snapshot?.Groups ?? []).OrderBy(g => g.Name, comparer).ThenBy(g => g.GroupId).ToList();
        var ids = groups.Select(g => g.GroupId).ToHashSet();
        var sections = groups.Select(g => (GroupId: (long?)g.GroupId, Title: g.Name)).ToList();
        if (snapshot is not null)
        {
            sections.Add((null, AppTexts.GroupsUngrouped));
        }

        // An app of a group that is not listed (cannot happen with one snapshot) is shown as ungrouped.
        var apps = (snapshot?.Apps ?? []).ToLookup(a => a.GroupId is { } id && ids.Contains(id) ? id : (long?)null);
        void Update(AppGroupSectionViewModel row, (long? GroupId, string Title) section) => row.Update(section.Title, apps[section.GroupId]);
        OrderedMerge.Apply(
            Sections,
            sections,
            row => row.GroupId,
            section => section.GroupId,
            section =>
            {
                var row = new AppGroupSectionViewModel(section.GroupId, this);
                Update(row, section);
                return row;
            },
            Update);
    }
}
