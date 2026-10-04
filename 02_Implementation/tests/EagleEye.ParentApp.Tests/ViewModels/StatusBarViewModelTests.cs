using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class StatusBarViewModelTests
{
    [Theory]
    [InlineData(ConnectionStatus.PairedConnected, "en-US", "Connected to kid-pc")]
    [InlineData(ConnectionStatus.PairedConnected, "de-DE", "Verbunden mit kid-pc")]
    [InlineData(ConnectionStatus.PairingConnecting, "en-US", "Connecting to kid-pc …")]
    [InlineData(ConnectionStatus.AwaitingCode, "en-US", "Connecting to kid-pc …")]
    [InlineData(ConnectionStatus.PairedConnecting, "de-DE", "Verbindung zu kid-pc wird hergestellt …")]
    [InlineData(ConnectionStatus.PairedDisconnected, "en-US", "Not connected to kid-pc")]
    [InlineData(ConnectionStatus.PairedDisconnected, "de-DE", "Nicht verbunden mit kid-pc")]
    [InlineData(ConnectionStatus.NotPaired, "en-US", "Not connected")]
    [InlineData(ConnectionStatus.NotPaired, "de-DE", "Nicht verbunden")]
    public void StatusText_PerState_IsLocalized(ConnectionStatus status, string culture, string expected)
    {
        var state = new ConnectionState(status, "kid-pc", null, ConnectionMessage.None);

        var text = TestSupport.InCulture(culture, () => StatusBarViewModel.Compose(state));

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(ConnectionStatus.PairedConnected, true)]
    [InlineData(ConnectionStatus.NotPaired, false)]
    [InlineData(ConnectionStatus.PairingConnecting, false)]
    [InlineData(ConnectionStatus.AwaitingCode, false)]
    [InlineData(ConnectionStatus.PairedConnecting, false)]
    [InlineData(ConnectionStatus.PairedDisconnected, false)]
    public void IsConnected_GreenOnlyWhenPairedConnected(ConnectionStatus status, bool expected)
    {
        var coordinator = new Mock<IConnectionCoordinator>();
        coordinator.SetupGet(c => c.State).Returns(new ConnectionState(status, "kid-pc", null, ConnectionMessage.None));

        var viewModel = new StatusBarViewModel(coordinator.Object, new ImmediateDispatcher());

        Assert.Equal(expected, viewModel.IsConnected);
    }

    [Fact]
    public void StateChanged_UpdatesViaDispatcher()
    {
        var coordinator = new Mock<IConnectionCoordinator>();
        coordinator.SetupGet(c => c.State).Returns(ConnectionState.Initial);
        var viewModel = new StatusBarViewModel(coordinator.Object, new ImmediateDispatcher());
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        coordinator.Raise(c => c.StateChanged += null, new ConnectionState(ConnectionStatus.PairedConnected, "kid-pc", "n", ConnectionMessage.None));

        Assert.True(viewModel.IsConnected);
        Assert.False(string.IsNullOrEmpty(viewModel.StatusText));
        Assert.Contains(nameof(StatusBarViewModel.StatusText), changed);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusBarViewModel(null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new StatusBarViewModel(Mock.Of<IConnectionCoordinator>(), null!));
    }
}
