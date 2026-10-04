using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Core.Data;

/// <summary>App settings (key/value in <c>AppSettings</c>).</summary>
public interface ISettingsStore
{
    /// <summary>The theme chosen by the parent, or <c>null</c> to follow the OS app mode.</summary>
    Task<ThemeMode?> GetThemeAsync();

    /// <summary>Stores the theme chosen by the parent.</summary>
    Task SetThemeAsync(ThemeMode theme);
}
