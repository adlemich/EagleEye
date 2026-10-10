using EagleEye.ParentApp.Core.Appearance;

namespace EagleEye.ParentApp.Services;

/// <summary>
/// Keeps the heading bar colours (US-004 ISSUE-007) in the app resources
/// <see cref="BackgroundKey"/> and <see cref="TextKey"/>, which the style <c>SectionHeader</c> uses as
/// dynamic resources. Background: the platform accent colour (Windows: the user's accent colour,
/// Android: the system accent colour), otherwise the app's primary colour; text: white or black,
/// whichever stays readable (<see cref="HeadingBarPalette"/>). Refreshed when the accent colour
/// changes (Windows) and whenever the window is activated.
/// </summary>
public sealed class HeadingBarColorService
{
    /// <summary>Resource key of the bar background colour.</summary>
    public const string BackgroundKey = "HeadingBarBackgroundColor";

    /// <summary>Resource key of the bar text colour.</summary>
    public const string TextKey = "HeadingBarTextColor";

#if WINDOWS
    private readonly Platforms.Windows.WindowsAccentColor _accent = new();
#endif

    /// <summary>Applies the colours now and keeps them current for the window.</summary>
    public void Attach(Window window)
    {
        Apply();
        window.Activated += (_, _) => Apply();
#if WINDOWS
        // Raised on a background thread; resources are changed on the UI thread.
        _accent.Changed += () => MainThread.BeginInvokeOnMainThread(Apply);
#endif
    }

    private void Apply()
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        var (background, text) = HeadingBarPalette.For(ReadPlatformAccent());
        application.Resources[BackgroundKey] = ToColor(background);
        application.Resources[TextKey] = ToColor(text);
    }

    private RgbColor? ReadPlatformAccent()
    {
#if WINDOWS
        return _accent.Read();
#elif ANDROID
        return Platforms.Android.AndroidAccentColor.Read();
#else
        // iOS and macOS (Mac Catalyst): the app's primary colour.
        return null;
#endif
    }

    private static Color ToColor(RgbColor color) => Color.FromRgb(color.R, color.G, color.B);
}
