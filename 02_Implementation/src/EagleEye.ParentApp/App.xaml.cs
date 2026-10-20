using EagleEye.ParentApp.Services;
using EagleEye.ParentApp.Views;

namespace EagleEye.ParentApp;

/// <summary>The parent app: one desktop window titled "EagleEye" with the main page.</summary>
public partial class App : Application
{
    /// <summary>Window title (product name, not translated).</summary>
    public const string WindowTitle = "EagleEye";

    private const double MinimumWidth = 900;
    private const double MinimumHeight = 600;
    private const double InitialWidth = 1100;
    private const double InitialHeight = 720;
#if MACCATALYST
    // Time for the hub writes started by the flush to leave the app before it ends (US-007 AC-32).
    private static readonly TimeSpan QuitFlushDelay = TimeSpan.FromMilliseconds(500);
#endif

    private readonly IServiceProvider _services;

    /// <summary>Creates the app.</summary>
    public App(IServiceProvider services)
    {
        InitializeComponent();
#if MACCATALYST
        // US-007 G-11: Mac-only styles after Styles.xaml (merged dictionaries are searched last-added first).
        Resources.MergedDictionaries.Add(new global::EagleEye.ParentApp.Resources.Styles.MacStyles());
#endif
        _services = services;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolved here, not in the constructor: the views need the styles loaded by InitializeComponent.
        var window = new Window(_services.GetRequiredService<MainPage>())
        {
            Title = WindowTitle,
            MinimumWidth = MinimumWidth,
            MinimumHeight = MinimumHeight,
            Width = InitialWidth,
            Height = InitialHeight,
        };
        _services.GetRequiredService<HeadingBarColorService>().Attach(window);
#if MACCATALYST
        // US-007 OQ-1 / ADR-020 §6: closing the window quits the app (macOS keeps apps running by default).
        window.Destroying += OnWindowDestroying;
#endif
        return window;
    }

#if MACCATALYST
    private async void OnWindowDestroying(object? sender, EventArgs e)
    {
        // async void is required for event handlers; nothing here throws.
        ((sender as Window)?.Page as MainPage)?.FlushPendingEdits();
        await Task.Delay(QuitFlushDelay);
        Quit();
    }
#endif
}
