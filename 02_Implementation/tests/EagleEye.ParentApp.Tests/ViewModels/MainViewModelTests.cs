using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class MainViewModelTests : IAsyncLifetime
{
    private readonly ParentDatabase _database = new("Data Source=:memory:");
    private readonly Mock<ISettingsStore> _settings = new();
    private readonly Mock<IThemeService> _theme = new();
    private readonly Mock<IConnectionCoordinator> _coordinator = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _coordinator.SetupGet(c => c.State).Returns(ConnectionState.Initial);
        var appearance = new AppearanceViewModel(_settings.Object, _theme.Object);
        var server = new ServerConnectionViewModel(_coordinator.Object, _dialogs.Object, new ImmediateDispatcher());
        _viewModel = new MainViewModel(_database, appearance, _coordinator.Object, server);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task StartAsync_NotPaired_ShowsHostDialog()
    {
        _coordinator.Setup(c => c.InitializeAsync()).ReturnsAsync(ConnectionState.Initial);

        await _viewModel.StartAsync();

        VerifyHostDialog(Times.Once());
    }

    [Fact]
    public async Task StartAsync_Paired_DoesNotShowHostDialog()
    {
        _coordinator.Setup(c => c.InitializeAsync())
            .ReturnsAsync(new ConnectionState(ConnectionStatus.PairedConnecting, "kid-pc", "n", ConnectionMessage.None));

        await _viewModel.StartAsync();

        VerifyHostDialog(Times.Never());
    }

    [Fact]
    public async Task StartAsync_InitializesDatabaseAndTheme()
    {
        _coordinator.Setup(c => c.InitializeAsync()).ReturnsAsync(ConnectionState.Initial);
        _settings.Setup(s => s.GetThemeAsync()).ReturnsAsync(ThemeMode.Dark);

        await _viewModel.StartAsync();

        Assert.Equal(1, await _database.GetSchemaVersionAsync());
        _theme.Verify(t => t.Apply(ThemeMode.Dark), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SecondCall_DoesNothing()
    {
        _coordinator.Setup(c => c.InitializeAsync()).ReturnsAsync(ConnectionState.Initial);
        await _viewModel.StartAsync();

        await _viewModel.StartAsync();

        _coordinator.Verify(c => c.InitializeAsync(), Times.Once);
    }

    [Fact]
    public void MenuItems_OnlySettings()
    {
        var titles = TestSupport.InCulture("de-DE", () => new MainViewModel(
            _database,
            new AppearanceViewModel(_settings.Object, _theme.Object),
            _coordinator.Object,
            new ServerConnectionViewModel(_coordinator.Object, _dialogs.Object, new ImmediateDispatcher())).MenuItems);

        Assert.Equal([new NavigationItem(MainViewModel.SettingsKey, "Einstellungen")], titles);
    }

    [Fact]
    public void SelectedItem_DefaultsToFirstAndCanBeSet()
    {
        var first = _viewModel.SelectedItem;
        var other = new NavigationItem("other", "Other");

        _viewModel.SelectedItem = other;

        Assert.Equal((_viewModel.MenuItems[0], other), (first, _viewModel.SelectedItem));
    }

    private void VerifyHostDialog(Times times)
    {
        _dialogs.Verify(
            d => d.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            times);
    }
}
