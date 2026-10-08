using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Data;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The main window (FR-APP-081, US-002 AC-8): navigation menu ("Settings", "Reports"), the
/// selected page, and the start sequence (database, theme, connection, host dialog if not paired).
/// </summary>
public sealed class MainViewModel(
    ParentDatabase database,
    AppearanceViewModel appearance,
    IConnectionCoordinator coordinator,
    ServerConnectionViewModel serverConnection) : ObservableObject
{
    /// <summary>Key of the settings page.</summary>
    public const string SettingsKey = "settings";

    /// <summary>Key of the reports page (US-004 AC-17).</summary>
    public const string ReportsKey = "reports";

    private NavigationItem? _selectedItem;
    private bool _started;

    /// <summary>The navigation menu entries.</summary>
    public IReadOnlyList<NavigationItem> MenuItems { get; } = [new NavigationItem(SettingsKey, AppTexts.MenuSettings), new NavigationItem(ReportsKey, AppTexts.MenuReports)];

    /// <summary>The selected menu entry (the first one by default).</summary>
    public NavigationItem SelectedItem
    {
        get => _selectedItem ?? MenuItems[0];
        set => SetProperty(ref _selectedItem, value);
    }

    /// <summary>
    /// Opens the database, applies the theme, starts the connection and, when the app is not
    /// paired, shows the host dialog (AC-11). Runs once.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await database.InitializeAsync();
        await appearance.InitializeAsync();
        var state = await coordinator.InitializeAsync();
        if (state.Status == ConnectionStatus.NotPaired)
        {
            await serverConnection.PromptForHostAsync();
        }
    }
}
