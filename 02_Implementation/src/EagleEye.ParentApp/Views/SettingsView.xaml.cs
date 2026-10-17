using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>
/// The settings page with the sections "Visual appearance", "Server connection" (US-002 AC-8), "User accounts on the
/// EagleEye PC" (US-003 AC-6) and "Time-elapsed warnings" (US-006 AC-13). Warning minutes are saved when the parent
/// leaves the field or presses Enter, texts when the parent leaves the box; <see cref="FlushPendingEdits"/> saves typed
/// values before the page changes (Unfocused does not fire then).
/// </summary>
public partial class SettingsView : ContentView
{
    private readonly WarningSettingsViewModel _warnings;

    /// <summary>Creates the view.</summary>
    public SettingsView(
        AppearanceViewModel appearance, ServerConnectionViewModel serverConnection, UserAccountsViewModel userAccounts, WarningSettingsViewModel warnings)
    {
        InitializeComponent();
        _warnings = warnings;
        AppearanceSection.BindingContext = appearance;
        ServerSection.BindingContext = serverConnection;
        UserAccountsSection.BindingContext = userAccounts;
        WarningsSection.BindingContext = warnings;
    }

    /// <summary>Saves typed warning minutes and texts (before another page is shown).</summary>
    public void FlushPendingEdits() => _warnings.FlushPendingEdits();

    private void OnEarlyMinutesUnfocused(object? sender, FocusEventArgs e) => _warnings.Early?.CommitMinutes();

    private void OnEarlyMinutesCompleted(object? sender, EventArgs e) => _warnings.Early?.CommitMinutes();

    private void OnEarlyTextUnfocused(object? sender, FocusEventArgs e) => _warnings.Early?.CommitText();

    private void OnFinalMinutesUnfocused(object? sender, FocusEventArgs e) => _warnings.Final?.CommitMinutes();

    private void OnFinalMinutesCompleted(object? sender, EventArgs e) => _warnings.Final?.CommitMinutes();

    private void OnFinalTextUnfocused(object? sender, FocusEventArgs e) => _warnings.Final?.CommitText();
}