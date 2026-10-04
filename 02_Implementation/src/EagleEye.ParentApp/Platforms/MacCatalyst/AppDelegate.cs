using Foundation;

namespace EagleEye.ParentApp;

/// <summary>Mac Catalyst application delegate (built on the MacBook, not part of US-002).</summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
