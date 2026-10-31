using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// One app on the App Groups page (US-008 AC-3): display name as on Reports, program path as a tooltip, and the
/// "≡" / right-click command "Move to …" (AC-12). An app that changes its section becomes a new entry of the new section.
/// </summary>
public sealed class GroupableAppViewModel : ObservableObject
{
    private string _displayName;

    internal GroupableAppViewModel(GroupableAppDto app, AppGroupSectionViewModel section, AppGroupsViewModel page)
    {
        ProgramPath = app.ProgramPath;
        _displayName = app.DisplayName;
        Section = section;
        MoveCommand = new AsyncRelayCommand(() => page.ChooseTargetAsync(this));
    }

    /// <summary>The app's identity, shown as a tooltip (AC-3).</summary>
    public string ProgramPath { get; }

    /// <summary>The display name, as on Reports.</summary>
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    /// <summary>The section the app is shown in.</summary>
    public AppGroupSectionViewModel Section { get; }

    /// <summary>"Move to …": lets the parent choose the target section (AC-12).</summary>
    public IAsyncRelayCommand MoveCommand { get; }

    /// <summary>Shows a newer name of the service.</summary>
    internal void Update(GroupableAppDto app) => DisplayName = app.DisplayName;
}
