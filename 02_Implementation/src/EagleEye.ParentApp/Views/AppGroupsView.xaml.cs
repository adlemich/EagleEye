using EagleEye.ParentApp.Core.ViewModels;

namespace EagleEye.ParentApp.Views;

/// <summary>
/// The App Groups page (US-008): forwards the drag-and-drop events of the MAUI recognizers to the view model (spike Step 1:
/// the dragged app is passed in-app; the text payload only gives the platform something to carry). Not unit-tested
/// (MAUI view); the logic is in <see cref="AppGroupsViewModel"/>.
/// </summary>
public partial class AppGroupsView : ContentView
{
    private readonly AppGroupsViewModel _viewModel;

    /// <summary>Creates the view.</summary>
    public AppGroupsView(AppGroupsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private static T? ContextOf<T>(object? sender)
        where T : class => (sender as BindableObject)?.BindingContext as T;

    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (ContextOf<GroupableAppViewModel>(sender) is { } app)
        {
            _viewModel.BeginDrag(app);
            e.Data.Text = app.ProgramPath;
        }
    }

    private void OnDropCompleted(object? sender, DropCompletedEventArgs e) => _viewModel.EndDrag();

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (ContextOf<AppGroupSectionViewModel>(sender) is { } section)
        {
            _viewModel.DragOver(section);
            e.AcceptedOperation = DataPackageOperation.Copy;
#if WINDOWS
            // US-009 TI-5: WinUI shows the accepted operation as "Copy" next to the pointer; the app is moved, so hide it.
            if (e.PlatformArgs?.DragEventArgs.DragUIOverride is { } dragUi)
            {
                dragUi.IsCaptionVisible = false;
                dragUi.IsGlyphVisible = false;
            }
#endif
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (ContextOf<AppGroupSectionViewModel>(sender) is { } section)
        {
            _viewModel.DragLeave(section);
        }
    }

    private async void OnDrop(object? sender, DropEventArgs e)
    {
        // async void is required for event handlers; DropAsync never throws (failed writes show the page's message).
        e.Handled = true;
        if (ContextOf<AppGroupSectionViewModel>(sender) is { } section)
        {
            await _viewModel.DropAsync(section);
        }
    }
}
