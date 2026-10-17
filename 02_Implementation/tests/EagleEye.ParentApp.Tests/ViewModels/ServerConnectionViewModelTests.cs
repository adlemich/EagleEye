using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class ServerConnectionViewModelTests
{
    private readonly Mock<IConnectionCoordinator> _coordinator = new();
    private readonly Mock<IDialogService> _dialogs = new();

    public ServerConnectionViewModelTests()
    {
        _coordinator.SetupGet(c => c.State).Returns(ConnectionState.Initial);
    }

    [Theory]
    [InlineData(ConnectionStatus.NotPaired, true, false, false, false, false, false)]
    [InlineData(ConnectionStatus.PairingConnecting, false, true, false, false, false, false)]
    [InlineData(ConnectionStatus.AwaitingCode, false, false, true, false, false, false)]
    [InlineData(ConnectionStatus.PairedConnecting, false, false, false, true, false, true)]
    [InlineData(ConnectionStatus.PairedConnected, false, false, false, true, true, false)]
    [InlineData(ConnectionStatus.PairedDisconnected, false, false, false, true, false, true)]
    public void Visibility_PerState(
        ConnectionStatus status, bool notPaired, bool connecting, bool awaitingCode, bool paired, bool canRemove, bool removeHint)
    {
        var viewModel = CreateViewModel(new ConnectionState(status, "kid-pc", "Dad's laptop", ConnectionMessage.None));

        Assert.Equal(
            (notPaired, connecting, awaitingCode, paired, canRemove, removeHint),
            (viewModel.IsNotPaired, viewModel.IsPairingConnecting, viewModel.IsAwaitingCode, viewModel.IsPaired, viewModel.CanRemove, viewModel.ShowRemoveHint));
    }

    [Theory]
    [InlineData(ConnectionStatus.PairedConnected, true)]
    [InlineData(ConnectionStatus.PairedDisconnected, false)]
    [InlineData(ConnectionStatus.NotPaired, false)]
    public void RemoveCommand_EnabledOnlyWhenConnected(ConnectionStatus status, bool expected)
    {
        var viewModel = CreateViewModel(new ConnectionState(status, "kid-pc", "n", ConnectionMessage.None));

        Assert.Equal(expected, viewModel.RemoveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(ConnectionStatus.NotPaired, "Not paired")]
    [InlineData(ConnectionStatus.AwaitingCode, "Pairing in progress")]
    [InlineData(ConnectionStatus.PairedDisconnected, "Paired")]
    public void PairingStatusText_PerState(ConnectionStatus status, string expected)
    {
        var viewModel = CreateViewModel(new ConnectionState(status, "kid-pc", "n", ConnectionMessage.None));

        Assert.Equal(expected, TestSupport.InCulture("en-US", () => viewModel.PairingStatusText));
    }

    [Fact]
    public void PairedTexts_ShowHostAndDeviceName()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedConnected, "kid-pc", "Dad's laptop", ConnectionMessage.None));

        var texts = TestSupport.InCulture("de-DE", () => (viewModel.PairedWithText, viewModel.ThisDeviceText));

        Assert.Equal(("Gekoppelt mit: kid-pc", "Dieses Gerät: Dad's laptop"), texts);
    }

    [Fact]
    public void StateChanged_ToNotPaired_PrefillsHost()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);

        _coordinator.Raise(c => c.StateChanged += null, ConnectionState.Initial with { Host = "kid-pc", LastMessage = ConnectionMessage.PairingLost });

        Assert.Equal("kid-pc", viewModel.HostInput);
    }

    [Fact]
    public void StateChanged_NotPairedWithoutHost_ClearsHost()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        viewModel.HostInput = "old";

        _coordinator.Raise(c => c.StateChanged += null, ConnectionState.Initial);

        Assert.Equal(string.Empty, viewModel.HostInput);
    }

    [Fact]
    public void StateChanged_Paired_KeepsHostInput()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        viewModel.HostInput = "typed";

        _coordinator.Raise(c => c.StateChanged += null, new ConnectionState(ConnectionStatus.AwaitingCode, "kid-pc", null, ConnectionMessage.None));

        Assert.Equal("typed", viewModel.HostInput);
    }

    [Fact]
    public void MessageText_WithMessage_IsShown()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial with { Host = "kid-pc", LastMessage = ConnectionMessage.Unreachable });

        var text = TestSupport.InCulture("en-US", () => viewModel.MessageText);

        Assert.Equal("Connection error: could not connect to the EagleEye service at kid-pc.", text);
        Assert.True(viewModel.HasMessage);
    }

    [Fact]
    public void HasMessage_NoMessage_IsFalse()
    {
        Assert.False(CreateViewModel(ConnectionState.Initial).HasMessage);
    }

    [Fact]
    public void DeviceNameInput_IsPrefilledWithMachineName()
    {
        Assert.Equal(Environment.MachineName, CreateViewModel(ConnectionState.Initial).DeviceNameInput);
    }

    [Fact]
    public async Task ConnectCommand_StartsPairingWithHostInput()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        viewModel.HostInput = "kid-pc";

        await viewModel.ConnectCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.BeginPairingAsync("kid-pc"), Times.Once);
    }

    [Fact]
    public async Task PairCommand_SubmitsCodeAndNameThenClearsCode()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        viewModel.CodeInput = "123456";
        viewModel.DeviceNameInput = "Dad's laptop";

        await viewModel.PairCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.SubmitCodeAsync("123456", "Dad's laptop"), Times.Once);
        Assert.Equal(string.Empty, viewModel.CodeInput);
    }

    [Fact]
    public async Task NewCodeCommand_RequestsNewCode()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);

        await viewModel.NewCodeCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.RequestNewCodeAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelCommand_CancelsPairing()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);

        await viewModel.CancelCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.CancelPairingAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveCommand_Confirmed_RemovesPairing()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedConnected, "kid-pc", "n", ConnectionMessage.None));
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        await viewModel.RemoveCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.RemovePairingAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveCommand_Cancelled_KeepsPairing()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedConnected, "kid-pc", "n", ConnectionMessage.None));
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await viewModel.RemoveCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.RemovePairingAsync(), Times.Never);
    }

    [Theory]
    [InlineData(ConnectionStatus.PairedDisconnected, ConnectionMessage.CertificateChanged, true)]
    [InlineData(ConnectionStatus.PairedConnecting, ConnectionMessage.CertificateChanged, true)]
    [InlineData(ConnectionStatus.PairedDisconnected, ConnectionMessage.Unreachable, true)]
    [InlineData(ConnectionStatus.PairedConnecting, ConnectionMessage.Unreachable, true)]
    [InlineData(ConnectionStatus.PairedDisconnected, ConnectionMessage.None, false)]
    [InlineData(ConnectionStatus.PairedConnecting, ConnectionMessage.None, false)]
    [InlineData(ConnectionStatus.PairedConnected, ConnectionMessage.None, false)]
    [InlineData(ConnectionStatus.PairedConnected, ConnectionMessage.RemoveFailed, false)]
    [InlineData(ConnectionStatus.NotPaired, ConnectionMessage.Unreachable, false)]
    [InlineData(ConnectionStatus.AwaitingCode, ConnectionMessage.None, false)]
    public void ForgetCommand_OfferedOnlyWhenTheStoredServiceCannotBeUsed(ConnectionStatus status, ConnectionMessage message, bool expected)
    {
        var viewModel = CreateViewModel(new ConnectionState(status, "kid-pc", "n", message));

        Assert.Equal((expected, expected), (viewModel.CanForget, viewModel.ForgetCommand.CanExecute(null)));
    }

    [Fact]
    public async Task ForgetCommand_Confirmed_ForgetsLocally()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedDisconnected, "kid-pc", "n", ConnectionMessage.CertificateChanged));
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        await viewModel.ForgetCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.ForgetPairingLocallyAsync(), Times.Once);
        _coordinator.Verify(c => c.RemovePairingAsync(), Times.Never);
    }

    [Fact]
    public async Task ForgetCommand_Cancelled_KeepsPairing()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedDisconnected, "kid-pc", "n", ConnectionMessage.Unreachable));
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await viewModel.ForgetCommand.ExecuteAsync(null);

        _coordinator.Verify(c => c.ForgetPairingLocallyAsync(), Times.Never);
    }

    [Theory]
    [InlineData("en-US", "Forget pairing on this device?",
        "The EagleEye PC kid-pc cannot be reached or its identity has changed. The pairing is deleted on this device only. Afterwards the app can be paired again with a new pairing code. The device entry may remain on the EagleEye PC; another parent app cannot remove it there at the moment.",
        "Forget pairing on this device", "Cancel")]
    [InlineData("de-DE", "Kopplung auf diesem Gerät löschen?",
        "Der EagleEye-PC kid-pc ist nicht erreichbar oder seine Identität hat sich geändert. Die Kopplung wird nur auf diesem Gerät gelöscht. Danach kann die App mit einem neuen Kopplungscode neu gekoppelt werden. Der Geräte-Eintrag kann auf dem EagleEye-PC bestehen bleiben; eine andere Eltern-App kann ihn dort derzeit nicht entfernen.",
        "Kopplung auf diesem Gerät löschen", "Abbrechen")]
    public async Task ForgetCommand_DialogTexts(string culture, string title, string message, string accept, string cancel)
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedDisconnected, "kid-pc", "n", ConnectionMessage.CertificateChanged));

        await TestSupport.InCulture(culture, () => viewModel.ForgetCommand.ExecuteAsync(null));

        _dialogs.Verify(d => d.ConfirmAsync(title, message, accept, cancel), Times.Once);
    }

    [Fact]
    public async Task RemoveCommand_AsksWithHostInMessage()
    {
        var viewModel = CreateViewModel(new ConnectionState(ConnectionStatus.PairedConnected, "kid-pc", "n", ConnectionMessage.None));

        await TestSupport.InCulture("en-US", () => viewModel.RemoveCommand.ExecuteAsync(null));

        _dialogs.Verify(d => d.ConfirmAsync(
            "Remove pairing?",
            "This PC will be detached from kid-pc. Connecting again requires a new pairing code.",
            "Remove pairing",
            "Cancel"), Times.Once);
    }

    [Fact]
    public async Task PromptForHostAsync_Confirmed_StartsPairingWithTrimmedHost()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        _dialogs.Setup(d => d.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(" kid-pc ");

        await viewModel.PromptForHostAsync();

        _coordinator.Verify(c => c.BeginPairingAsync("kid-pc"), Times.Once);
        Assert.Equal("kid-pc", viewModel.HostInput);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task PromptForHostAsync_Cancelled_DoesNotConnect(string? answer)
    {
        var viewModel = CreateViewModel(ConnectionState.Initial);
        _dialogs.Setup(d => d.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(answer);

        await viewModel.PromptForHostAsync();

        _coordinator.Verify(c => c.BeginPairingAsync(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task PromptForHostAsync_ShowsLocalizedDialogWithLastHost()
    {
        var viewModel = CreateViewModel(ConnectionState.Initial with { Host = "kid-pc" });

        await TestSupport.InCulture("de-DE", () => viewModel.PromptForHostAsync());

        _dialogs.Verify(d => d.PromptAsync(
            "Mit dem EagleEye-PC verbinden",
            "Hostname oder IP-Adresse des EagleEye-PCs",
            "Verbinden",
            "Abbrechen",
            "z. B. kinder-pc",
            "kid-pc"), Times.Once);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new ServerConnectionViewModel(null!, _dialogs.Object, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ServerConnectionViewModel(_coordinator.Object, null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ServerConnectionViewModel(_coordinator.Object, _dialogs.Object, null!));
    }

    private ServerConnectionViewModel CreateViewModel(ConnectionState state)
    {
        _coordinator.SetupGet(c => c.State).Returns(state);
        return new ServerConnectionViewModel(_coordinator.Object, _dialogs.Object, new ImmediateDispatcher());
    }
}
