using Android.App;
using Android.Runtime;

namespace EagleEye.ParentApp;

/// <summary>Android application class.</summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>Called by the Android runtime.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
