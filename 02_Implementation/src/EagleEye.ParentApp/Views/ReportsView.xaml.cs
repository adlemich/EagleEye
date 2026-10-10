using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>The Reports page: daily app usage per account under parental control (US-004 AC-17 to AC-23).</summary>
public partial class ReportsView : ContentView
{
    /// <summary>Creates the view.</summary>
    public ReportsView(ReportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
