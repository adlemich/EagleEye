using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// One section of the App Groups page (US-008 AC-2, AC-3): a group with "Rename" and "Delete", or "Not in any group"
/// (<see cref="GroupId"/> = null). Its apps are sorted by display name in the parent's culture ignoring case, then by
/// path; an empty section shows <see cref="EmptyText"/>. <see cref="IsDropTarget"/> is the drag highlight (AC-11).
/// </summary>
public sealed class AppGroupSectionViewModel : ObservableObject
{
    private readonly AppGroupsViewModel _page;
    private string _title = string.Empty;
    private bool _isDropTarget;

    internal AppGroupSectionViewModel(long? groupId, AppGroupsViewModel page)
    {
        GroupId = groupId;
        _page = page;
        RenameCommand = new AsyncRelayCommand(() => page.RenameAsync(this));
        DeleteCommand = new AsyncRelayCommand(() => page.DeleteAsync(this));
    }

    /// <summary>The group, or null for "Not in any group".</summary>
    public long? GroupId { get; }

    /// <summary>Whether the section is a group (with "Rename" and "Delete").</summary>
    public bool IsGroup => GroupId is not null;

    /// <summary>The group name, or "Not in any group".</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>The apps of the section, sorted.</summary>
    public ObservableCollection<GroupableAppViewModel> Apps { get; } = [];

    /// <summary>The section has no apps.</summary>
    public bool ShowEmpty => Apps.Count == 0;

    /// <summary>"No apps in this group. Drag apps here." or "All apps are in a group." (AC-3).</summary>
    public string EmptyText => IsGroup ? AppTexts.GroupsEmpty : AppTexts.GroupsAllGrouped;

    /// <summary>Whether an app is dragged over the section (AC-11).</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    /// <summary>"Rename" (AC-9).</summary>
    public IAsyncRelayCommand RenameCommand { get; }

    /// <summary>"Delete" (AC-10).</summary>
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>Shows the title and the apps of a newer snapshot.</summary>
    internal void Update(string title, IEnumerable<GroupableAppDto> apps)
    {
        Title = title;
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var ordered = apps.OrderBy(a => a.DisplayName, comparer).ThenBy(a => a.ProgramPath, StringComparer.Ordinal).ToList();
        OrderedMerge.Apply(Apps, ordered, row => row.ProgramPath, app => app.ProgramPath, app => new GroupableAppViewModel(app, this, _page), (row, app) => row.Update(app));
        OnPropertyChanged(nameof(ShowEmpty));
    }
}
