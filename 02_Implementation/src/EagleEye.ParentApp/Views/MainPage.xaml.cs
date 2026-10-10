using System.Diagnostics;
using EagleEye.ParentApp.Core;
using EagleEye.ParentApp.Core.ViewModels;
using Microsoft.Data.Sqlite;

namespace EagleEye.ParentApp.Views;

/// <summary>The main window content: menu, settings, rules or reports page, status bar (US-002 AC-8, AC-10; US-004 AC-17; US-005 AC-1).</summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsView _settings;
    private readonly ReportsView _reports;
    private readonly RulesView _rules;

    /// <summary>Creates the page.</summary>
    public MainPage(MainViewModel viewModel, SettingsView settings, RulesView rules, ReportsView reports, StatusBarView statusBar)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        _reports = reports;
        _rules = rules;
        BindingContext = viewModel;
        ShowSelectedPage();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedItem))
            {
                ShowSelectedPage();
            }
        };
        StatusBarRegion.Content = statusBar;
        Loaded += OnLoaded;
    }

    private void ShowSelectedPage()
    {
        if (ReferenceEquals(ContentRegion.Content, _rules))
        {
            // US-005 AC-15: typed values are saved before the Rules page is left (Unfocused does not fire then).
            _rules.FlushPendingEdits();
        }

        ContentRegion.Content = _viewModel.SelectedItem.Key switch
        {
            MainViewModel.ReportsKey => _reports,
            MainViewModel.RulesKey => _rules,
            _ => _settings,
        };
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
