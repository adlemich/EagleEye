using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>The settings page with the sections "Visual appearance" and "Server connection" (US-002 AC-8).</summary>
public partial class SettingsView : ContentView
{
    /// <summary>Creates the view.</summary>
    public SettingsView(AppearanceViewModel appearance, ServerConnectionViewModel serverConnection)
    {
        InitializeComponent();
        AppearanceSection.BindingContext = appearance;
        ServerSection.BindingContext = serverConnection;
    }
}
