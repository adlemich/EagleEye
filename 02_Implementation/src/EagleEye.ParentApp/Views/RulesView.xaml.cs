using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>
/// The Rules page: break times and display text per account under parental control (US-005 AC-1 to AC-19). Times are
/// saved when the parent leaves the field or presses Enter, the display text when the parent leaves the box (AC-8,
/// AC-15); <see cref="FlushPendingEdits"/> saves typed values before the page changes (Unfocused does not fire then).
/// </summary>
public partial class RulesView : ContentView
{
    private readonly RulesViewModel _viewModel;

    /// <summary>Creates the view.</summary>
    public RulesView(RulesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>Saves typed times and the display text (before another page is shown).</summary>
    public void FlushPendingEdits() => _viewModel.FlushPendingEdits();

    private static BreakTimeRowViewModel? RowOf(object? sender) => (sender as BindableObject)?.BindingContext as BreakTimeRowViewModel;

    private void OnStartUnfocused(object? sender, FocusEventArgs e) => RowOf(sender)?.CommitStart();

    private void OnStartCompleted(object? sender, EventArgs e) => RowOf(sender)?.CommitStart();

    private void OnEndUnfocused(object? sender, FocusEventArgs e) => RowOf(sender)?.CommitEnd();

    private void OnEndCompleted(object? sender, EventArgs e) => RowOf(sender)?.CommitEnd();

    private void OnDisplayTextUnfocused(object? sender, FocusEventArgs e) => _viewModel.CommitDisplayText();
}
