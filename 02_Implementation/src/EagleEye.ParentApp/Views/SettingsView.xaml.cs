using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>
/// The settings page with the sections "Visual appearance", "Server connection" (US-002 AC-8) and
/// "User accounts on the EagleEye PC" (US-003 AC-6).
/// </summary>
public partial class SettingsView : ContentView
{
    /// <summary>Creates the view.</summary>
    public SettingsView(AppearanceViewModel appearance, ServerConnectionViewModel serverConnection, UserAccountsViewModel userAccounts)
    {
        InitializeComponent();
        AppearanceSection.BindingContext = appearance;
        ServerSection.BindingContext = serverConnection;
        UserAccountsSection.BindingContext = userAccounts;
    }
}
