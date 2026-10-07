using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Data;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// "Visual appearance" section (US-002 AC-9): a light/dark switch. The initial value is the stored
/// choice or, on first start, the OS app mode. A change is applied immediately and stored.
/// </summary>
public sealed class AppearanceViewModel(ISettingsStore settings, IThemeService themeService) : ObservableObject
{
    private bool _isDarkMode;
    private bool _initialized;

    /// <summary>Whether dark mode is selected.</summary>
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (!SetProperty(ref _isDarkMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemeLabel));
            if (_initialized)
            {
                var theme = value ? ThemeMode.Dark : ThemeMode.Light;
                themeService.Apply(theme);
                PendingSave = settings.SetThemeAsync(theme);
            }
        }
    }

    /// <summary>The label next to the switch ("Light" / "Dark").</summary>
    public string ThemeLabel => _isDarkMode ? AppTexts.ThemeDark : AppTexts.ThemeLight;

    /// <summary>The last save operation (awaited by tests).</summary>
    internal Task PendingSave { get; private set; } = Task.CompletedTask;

    /// <summary>Loads the stored theme and applies it; without a stored theme the app follows the OS.</summary>
    public async Task InitializeAsync()
    {
        // No ConfigureAwait(false): the property change below must be raised on the UI thread.
        var stored = await settings.GetThemeAsync();
        if (stored is { } theme)
        {
            themeService.Apply(theme);
        }

        IsDarkMode = (stored ?? themeService.SystemTheme) == ThemeMode.Dark;
        _initialized = true;
    }
}
