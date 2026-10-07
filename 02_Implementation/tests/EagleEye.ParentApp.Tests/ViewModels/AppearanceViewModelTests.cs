using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppearanceViewModelTests
{
    private readonly Mock<ISettingsStore> _settings = new();
    private readonly Mock<IThemeService> _theme = new();
    private readonly AppearanceViewModel _viewModel;

    public AppearanceViewModelTests()
    {
        _viewModel = new AppearanceViewModel(_settings.Object, _theme.Object);
    }

    [Theory]
    [InlineData(ThemeMode.Dark, true)]
    [InlineData(ThemeMode.Light, false)]
    public async Task InitializeAsync_NothingStored_FollowsSystemTheme(ThemeMode system, bool expectedDark)
    {
        _theme.SetupGet(t => t.SystemTheme).Returns(system);

        await _viewModel.InitializeAsync();

        Assert.Equal(expectedDark, _viewModel.IsDarkMode);
        _theme.Verify(t => t.Apply(It.IsAny<ThemeMode>()), Times.Never);
    }

    [Theory]
    [InlineData(ThemeMode.Dark, ThemeMode.Light, true)]
    [InlineData(ThemeMode.Light, ThemeMode.Dark, false)]
    public async Task InitializeAsync_Stored_UsesAndAppliesStoredTheme(ThemeMode stored, ThemeMode system, bool expectedDark)
    {
        _settings.Setup(s => s.GetThemeAsync()).ReturnsAsync(stored);
        _theme.SetupGet(t => t.SystemTheme).Returns(system);

        await _viewModel.InitializeAsync();

        Assert.Equal(expectedDark, _viewModel.IsDarkMode);
        _theme.Verify(t => t.Apply(stored), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotStoreTheme()
    {
        _theme.SetupGet(t => t.SystemTheme).Returns(ThemeMode.Dark);

        await _viewModel.InitializeAsync();

        _settings.Verify(s => s.SetThemeAsync(It.IsAny<ThemeMode>()), Times.Never);
    }

    [Theory]
    [InlineData(true, ThemeMode.Dark)]
    [InlineData(false, ThemeMode.Light)]
    public async Task IsDarkMode_Toggled_AppliesImmediatelyAndStores(bool dark, ThemeMode expected)
    {
        _theme.SetupGet(t => t.SystemTheme).Returns(dark ? ThemeMode.Light : ThemeMode.Dark);
        _settings.Setup(s => s.SetThemeAsync(It.IsAny<ThemeMode>())).Returns(Task.CompletedTask);
        await _viewModel.InitializeAsync();

        _viewModel.IsDarkMode = dark;
        await _viewModel.PendingSave;

        _theme.Verify(t => t.Apply(expected), Times.Once);
        _settings.Verify(s => s.SetThemeAsync(expected), Times.Once);
    }

    [Fact]
    public async Task IsDarkMode_SameValue_DoesNothing()
    {
        _theme.SetupGet(t => t.SystemTheme).Returns(ThemeMode.Dark);
        await _viewModel.InitializeAsync();

        _viewModel.IsDarkMode = true;

        _settings.Verify(s => s.SetThemeAsync(It.IsAny<ThemeMode>()), Times.Never);
    }

    [Theory]
    [InlineData(true, "en-US", "Dark")]
    [InlineData(false, "en-US", "Light")]
    [InlineData(true, "de-DE", "Dunkel")]
    [InlineData(false, "de-DE", "Hell")]
    public void ThemeLabel_FollowsSwitch(bool dark, string culture, string expected)
    {
        _viewModel.IsDarkMode = dark;

        Assert.Equal(expected, TestSupport.InCulture(culture, () => _viewModel.ThemeLabel));
    }

    [Fact]
    public void IsDarkMode_Changed_RaisesThemeLabelChanged()
    {
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _viewModel.IsDarkMode = true;

        Assert.Equal([nameof(AppearanceViewModel.IsDarkMode), nameof(AppearanceViewModel.ThemeLabel)], changed);
    }
}
