using EagleEye.ParentApp.Core.Appearance;

namespace EagleEye.ParentApp.Platforms.Android;

/// <summary>The Android system accent colour (Material You, from the wallpaper; API 31+, the app needs 34).</summary>
public static class AndroidAccentColor
{
    /// <summary>The current system accent colour (tone 600 of the primary accent palette).</summary>
    public static RgbColor Read()
    {
        var color = new global::Android.Graphics.Color(
            global::Android.App.Application.Context.GetColor(global::Android.Resource.Color.SystemAccent1600));
        return new RgbColor(color.R, color.G, color.B);
    }
}
