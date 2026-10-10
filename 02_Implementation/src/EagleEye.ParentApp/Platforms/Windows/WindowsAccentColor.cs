using EagleEye.ParentApp.Core.Appearance;
using Windows.UI.ViewManagement;

namespace EagleEye.ParentApp.Platforms.Windows;

/// <summary>The user's Windows accent colour (Settings → Personalization → Colors), and its changes.</summary>
public sealed class WindowsAccentColor
{
    // Kept as a field: the ColorValuesChanged subscription lives only as long as this instance.
    private readonly UISettings _settings = new();

    /// <summary>Creates the reader and subscribes to colour changes.</summary>
    public WindowsAccentColor()
    {
        _settings.ColorValuesChanged += (_, _) => Changed?.Invoke();
    }

    /// <summary>Raised (on a background thread) when Windows colours, e.g. the accent colour, change.</summary>
    public event Action? Changed;

    /// <summary>The current accent colour.</summary>
    public RgbColor Read()
    {
        var color = _settings.GetColorValue(UIColorType.Accent);
        return new RgbColor(color.R, color.G, color.B);
    }
}
