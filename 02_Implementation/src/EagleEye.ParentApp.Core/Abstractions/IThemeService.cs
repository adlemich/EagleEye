namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Reads the OS app mode and applies the app's theme (US-002 AC-9).</summary>
public interface IThemeService
{
    /// <summary>The app mode (light/dark) of the current OS user.</summary>
    ThemeMode SystemTheme { get; }

    /// <summary>Applies the theme to the whole app immediately.</summary>
    void Apply(ThemeMode theme);
}
