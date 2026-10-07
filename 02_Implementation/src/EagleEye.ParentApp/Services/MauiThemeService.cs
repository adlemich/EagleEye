using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>Theme via <see cref="Application.UserAppTheme"/>; the OS app mode via <see cref="Application.PlatformAppTheme"/>.</summary>
public sealed class MauiThemeService : IThemeService
{
    /// <inheritdoc />
    public ThemeMode SystemTheme =>
        Application.Current?.PlatformAppTheme == AppTheme.Dark ? ThemeMode.Dark : ThemeMode.Light;

    /// <inheritdoc />
    public void Apply(ThemeMode theme)
    {
        if (Application.Current is { } application)
        {
            application.UserAppTheme = theme == ThemeMode.Dark ? AppTheme.Dark : AppTheme.Light;
        }
    }
}
