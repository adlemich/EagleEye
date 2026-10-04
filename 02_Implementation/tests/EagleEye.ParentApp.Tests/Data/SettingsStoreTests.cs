using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Data;
using Xunit;

namespace EagleEye.ParentApp.Tests.Data;

public sealed class SettingsStoreTests : IAsyncLifetime
{
    private readonly ParentDatabase _database = new("Data Source=:memory:");
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _store = new SettingsStore(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task GetThemeAsync_Absent_ReturnsNull()
    {
        Assert.Null(await _store.GetThemeAsync());
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public async Task GetThemeAsync_AfterSet_ReturnsTheme(ThemeMode theme)
    {
        await _store.SetThemeAsync(theme);

        Assert.Equal(theme, await _store.GetThemeAsync());
    }

    [Fact]
    public async Task SetThemeAsync_Twice_OverwritesValue()
    {
        await _store.SetThemeAsync(ThemeMode.Dark);

        await _store.SetThemeAsync(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, await _store.GetThemeAsync());
    }

    [Theory]
    [InlineData(ThemeMode.Light, "light")]
    [InlineData(ThemeMode.Dark, "dark")]
    public async Task SetThemeAsync_StoresLowerCaseValue(ThemeMode theme, string expected)
    {
        await _store.SetThemeAsync(theme);

        Assert.Equal(expected, await _store.GetAsync(SettingsStore.ThemeKey));
    }

    [Fact]
    public async Task GetThemeAsync_UnknownValue_ReturnsNull()
    {
        await _store.SetAsync(SettingsStore.ThemeKey, "purple");

        Assert.Null(await _store.GetThemeAsync());
    }
}
