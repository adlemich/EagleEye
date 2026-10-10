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

    private readonly IServiceProvider _services;

    /// <summary>Creates the app.</summary>
    public App(IServiceProvider services)
    {
        InitializeComponent();
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
        return window;
    }
}
