using System.Diagnostics;
using EagleEye.ParentApp.Core;
using EagleEye.ParentApp.Core.ViewModels;
using Microsoft.Data.Sqlite;

namespace EagleEye.ParentApp.Views;

/// <summary>The main window content: menu, settings page and status bar (US-002 AC-8, AC-10).</summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    /// <summary>Creates the page.</summary>
    public MainPage(MainViewModel viewModel, SettingsView settings, StatusBarView statusBar)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        ContentRegion.Content = settings;
        StatusBarRegion.Content = statusBar;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        // async void is required for event handlers; failures are handled here.
        Loaded -= OnLoaded;
        try
        {
            await _viewModel.StartAsync();
        }
        catch (Exception ex) when (ex is SqliteException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            await DisplayAlertAsync(App.WindowTitle, AppTexts.ErrorStartup, AppTexts.OkButton);
        }
    }
}
