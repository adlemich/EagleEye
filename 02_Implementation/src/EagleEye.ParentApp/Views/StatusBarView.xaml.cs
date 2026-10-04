using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>The status bar at the bottom of the main window (US-002 AC-10).</summary>
public partial class StatusBarView : ContentView
{
    /// <summary>Creates the view.</summary>
    public StatusBarView(StatusBarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
