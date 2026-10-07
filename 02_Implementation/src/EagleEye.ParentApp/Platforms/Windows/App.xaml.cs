namespace EagleEye.ParentApp.WinUI;

/// <summary>WinUI entry point of the Windows parent app (unpackaged, ADR-009).</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes the WinUI application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
