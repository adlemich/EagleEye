using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ConnectionCoordinatorTests : IAsyncLifetime
{
    private const string Host = "kid-pc";
    private const string DeviceId = "6f1f8a0e-4c2b-4bd8-9d5e-0b6f1b2c3d4e";
    private const string DeviceName = "Dad's laptop";
    private const string Token = "token-abc";
    private const string Code = "123456";

    private static readonly PairingStatusDto Paired = new(true, DeviceId, DeviceName);
    private static readonly PairingStatusDto NotPaired = new(false, null, null);

    private readonly Mock<IParentHubClientFactory> _factory = new();
    private readonly Mock<IPairingStore> _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly Queue<Mock<IParentHubClient>> _clients = new();
    private readonly List<(string? Token, CertificateTrustPolicy Trust)> _created = [];
    private readonly ConnectionCoordinator _coordinator;

    public ConnectionCoordinatorTests()
    {
        _factory
            .Setup(f => f.Create(It.IsAny<HostAddress>(), It.IsAny<string?>(), It.IsAny<CertificateTrustPolicy>()))
            .Returns((HostAddress _, string? token, CertificateTrustPolicy trust) =>
            {
                _created.Add((token, trust));
                // A connected client has seen the service certificate.
                trust.Validate(TestSupport.Certificate);
                return _clients.Count > 0 ? _clients.Dequeue().Object : NewClient().Object;
            });
        _coordinator = new ConnectionCoordinator(_factory.Object, _store.Object, _time);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _coordinator.DisposeAsync();

    // ---------- Initialize ----------

    [Fact]
    public async Task InitializeAsync_NoStoredPairing_IsNotPaired()
    {
        var state = await _coordinator.InitializeAsync();

        Assert.Equal(ConnectionState.Initial, state);
    }

    [Fact]
    public async Task InitializeAsync_StoredPairingAndServiceConfirms_IsPairedConnected()
    {
        StorePairing();
        EnqueueClient(status: Paired);

        await _coordinator.InitializeAsync();
        await _coordinator.PairedLoop;

        Assert.Equal(new ConnectionState(ConnectionStatus.PairedConnected, Host, DeviceName, ConnectionMessage.None), _coordinator.State);
    }

    [Fact]
    public async Task InitializeAsync_StoredPairing_ConnectsWithTokenAndPinnedThumbprint()
    {
        StorePairing();
        EnqueueClient(status: Paired);

        await _coordinator.InitializeAsync();

        Assert.Equal((Token, TestSupport.Thumbprint), (_created[0].Token, _created[0].Trust.PinnedThumbprint));
    }

    [Fact]
    public async Task InitializeAsync_StoredPairing_ReportsConnectingFirst()
    {
        StorePairing();
        var start = new TaskCompletionSource();
        EnqueueClient(status: Paired, start: start.Task);

        var state = await _coordinator.InitializeAsync();

        Assert.Equal(ConnectionStatus.PairedConnecting, state.Status);
        start.SetResult();
    }

    [Fact]
    public async Task InitializeAsync_ServiceSaysNotPaired_DeletesPairingAndReportsLost()
    {
        StorePairing();
        EnqueueClient(status: NotPaired);

        await _coordinator.InitializeAsync();
        await _coordinator.PairedLoop;

        _store.Verify(s => s.DeleteAsync(), Times.Once);
        Assert.Equal(ConnectionState.Initial with { Host = Host, LastMessage = ConnectionMessage.PairingLost }, _coordinator.State);
    }

    [Fact]
    public async Task InitializeAsync_StoredHostInvalid_DeletesPairingAndIsNotPaired()
    {
        StorePairing(host: "not a host");

        var state = await _coordinator.InitializeAsync();

        _store.Verify(s => s.DeleteAsync(), Times.Once);
        Assert.Equal(ConnectionState.Initial, state);
    }

    [Fact]
    public async Task InitializeAsync_Unreachable_IsPairedDisconnectedWithMessage()
    {
        StorePairing();
        EnqueueClient(startFails: true);

        await _coordinator.InitializeAsync();

        Assert.Equal((ConnectionStatus.PairedDisconnected, ConnectionMessage.Unreachable), (_coordinator.State.Status, _coordinator.State.LastMessage));
    }

    [Fact]
    public async Task InitializeAsync_PinMismatch_ReportsCertificateChanged()
    {
        StorePairing(thumbprint: "00");
        EnqueueClient(startFails: true);

        await _coordinator.InitializeAsync();

        Assert.Equal(ConnectionMessage.CertificateChanged, _coordinator.State.LastMessage);
    }

    [Fact]
    public async Task InitializeAsync_StatusCallFails_RetriesAndDisposesClient()
    {
        StorePairing();
        var failing = EnqueueClient(statusFails: true);

        await _coordinator.InitializeAsync();

        failing.Verify(c => c.DisposeAsync(), Times.Once);
        Assert.Equal(ConnectionStatus.PairedDisconnected, _coordinator.State.Status);
    }

    [Fact]
    public async Task InitializeAsync_Unreachable_RetriesAfterBackoffAndConnects()
    {
        StorePairing();
        EnqueueClient(startFails: true);
        EnqueueClient(status: Paired);

        await _coordinator.InitializeAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await _coordinator.PairedLoop;

        Assert.Equal((ConnectionStatus.PairedConnected, 2), (_coordinator.State.Status, _created.Count));
    }

    [Fact]
    public async Task InitializeAsync_Unreachable_DoesNotRetryBeforeBackoff()
    {
        StorePairing();
        EnqueueClient(startFails: true);

        await _coordinator.InitializeAsync();
        _time.Advance(TimeSpan.FromMilliseconds(999));

        Assert.Single(_created);
    }

    [Fact]
    public async Task InitializeAsync_NeverConnectedWithoutPairedStatus()
    {
        StorePairing();
        EnqueueClient(status: NotPaired);
        var states = RecordStates();

        await _coordinator.InitializeAsync();
        await _coordinator.PairedLoop;

        Assert.DoesNotContain(states, s => s.Status == ConnectionStatus.PairedConnected);
    }

    // ---------- Paired connection events ----------

    [Fact]
    public async Task Reconnecting_WhenConnected_IsPairedDisconnected()
    {
        var client = await ConnectPairedAsync();

        client.Raise(c => c.Reconnecting += null);

        Assert.Equal(ConnectionStatus.PairedDisconnected, _coordinator.State.Status);
    }

    [Fact]
    public async Task Reconnected_ServiceConfirms_IsPairedConnected()
    {
        var client = await ConnectPairedAsync();
        client.Raise(c => c.Reconnecting += null);

        client.Raise(c => c.Reconnected += null);

        Assert.Equal(ConnectionStatus.PairedConnected, _coordinator.State.Status);
    }

    [Fact]
    public async Task Reconnected_ServiceSaysNotPaired_DeletesPairingAndReportsLost()
    {
        var client = await ConnectPairedAsync();
        client.Raise(c => c.Reconnecting += null);
        client.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NotPaired);

        client.Raise(c => c.Reconnected += null);

        _store.Verify(s => s.DeleteAsync(), Times.Once);
        client.Verify(c => c.DisposeAsync(), Times.Once);
        Assert.Equal(ConnectionMessage.PairingLost, _coordinator.State.LastMessage);
    }

    [Fact]
    public async Task Reconnected_StatusCallFails_StaysDisconnected()
    {
        var client = await ConnectPairedAsync();
        client.Raise(c => c.Reconnecting += null);
        client.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        client.Raise(c => c.Reconnected += null);

        Assert.Equal(ConnectionStatus.PairedDisconnected, _coordinator.State.Status);
    }

    [Fact]
    public async Task Closed_WhenConnected_StartsNewConnectLoop()
    {
        var client = await ConnectPairedAsync();
        EnqueueClient(status: Paired);

        client.Raise(c => c.Closed += null);
        await _coordinator.PairedLoop;

        Assert.Equal((ConnectionStatus.PairedConnected, 2), (_coordinator.State.Status, _created.Count));
    }

    [Fact]
    public async Task Events_OfReplacedClient_AreIgnored()
    {
        var old = await ConnectPairedAsync();
        EnqueueClient(status: Paired);
        old.Raise(c => c.Closed += null);
        await _coordinator.PairedLoop;

        old.Raise(c => c.Reconnecting += null);

        Assert.Equal(ConnectionStatus.PairedConnected, _coordinator.State.Status);
    }

    [Fact]
    public async Task ReconnectedAndClosed_OfReplacedClient_AreIgnored()
    {
        var old = await ConnectPairedAsync();
        EnqueueClient(status: Paired);
        old.Raise(c => c.Closed += null);
        await _coordinator.PairedLoop;
        old.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NotPaired);

        old.Raise(c => c.Reconnected += null);
        old.Raise(c => c.Closed += null);

        Assert.Equal((ConnectionStatus.PairedConnected, 2), (_coordinator.State.Status, _created.Count));
        _store.Verify(s => s.DeleteAsync(), Times.Never);
    }

    // ---------- Pairing ----------

    [Fact]
    public async Task BeginPairingAsync_Reachable_IsAwaitingCodeAndRequestsCode()
    {
        var client = EnqueueClient();

        await _coordinator.BeginPairingAsync(" kid-pc ");

        client.Verify(c => c.StartPairingAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new ConnectionState(ConnectionStatus.AwaitingCode, Host, null, ConnectionMessage.None), _coordinator.State);
    }

    [Fact]
    public async Task BeginPairingAsync_UsesTrustOnFirstUseWithoutToken()
    {
        EnqueueClient();

        await _coordinator.BeginPairingAsync(Host);

        Assert.Equal((null, null), (_created[0].Token, _created[0].Trust.PinnedThumbprint));
    }

    [Fact]
    public async Task BeginPairingAsync_ReportsPairingConnectingWhileConnecting()
    {
        var start = new TaskCompletionSource();
        EnqueueClient(start: start.Task);

        var pairing = _coordinator.BeginPairingAsync(Host);
        var during = _coordinator.State.Status;
        start.SetResult();
        await pairing;

        Assert.Equal(ConnectionStatus.PairingConnecting, during);
    }

    [Fact]
    public async Task BeginPairingAsync_Unreachable_IsNotPairedWithErrorAndHost()
    {
        var client = EnqueueClient(startFails: true);

        await _coordinator.BeginPairingAsync(Host);

        client.Verify(c => c.DisposeAsync(), Times.Once);
        Assert.Equal(ConnectionState.Initial with { Host = Host, LastMessage = ConnectionMessage.Unreachable }, _coordinator.State);
    }

    [Fact]
    public async Task BeginPairingAsync_StartPairingFails_IsNotPairedWithError()
    {
        var client = EnqueueClient();
        client.Setup(c => c.StartPairingAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        await _coordinator.BeginPairingAsync(Host);

        Assert.Equal(ConnectionMessage.Unreachable, _coordinator.State.LastMessage);
    }

    [Theory]
    [InlineData("https://kid-pc", "https://kid-pc")]
    [InlineData(" kid pc ", "kid pc")]
    [InlineData(null, null)]
    public async Task BeginPairingAsync_InvalidHost_ReportsInvalidHostWithoutConnecting(string? input, string? expectedHost)
    {
        await _coordinator.BeginPairingAsync(input);

        Assert.Empty(_created);
        Assert.Equal(ConnectionState.Initial with { Host = expectedHost, LastMessage = ConnectionMessage.InvalidHost }, _coordinator.State);
    }

    [Fact]
    public async Task BeginPairingAsync_WhenAlreadyPairing_DoesNothing()
    {
        EnqueueClient();
        await _coordinator.BeginPairingAsync(Host);

        await _coordinator.BeginPairingAsync("other-pc");

        Assert.Single(_created);
    }

    [Fact]
    public async Task RequestNewCodeAsync_AwaitingCode_RequestsAnotherCodeAndClearsMessage()
    {
        var client = await BeginPairingAsync();
        client.Setup(c => c.SubmitPairingCodeAsync(Code, DeviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PairingResultDto(PairingOutcome.WrongCode, null, null));
        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        await _coordinator.RequestNewCodeAsync();

        client.Verify(c => c.StartPairingAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(ConnectionMessage.None, _coordinator.State.LastMessage);
    }

    [Fact]
    public async Task RequestNewCodeAsync_Fails_IsNotPairedWithError()
    {
        var client = await BeginPairingAsync();
        client.Setup(c => c.StartPairingAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        await _coordinator.RequestNewCodeAsync();

        Assert.Equal((ConnectionStatus.NotPaired, ConnectionMessage.Unreachable), (_coordinator.State.Status, _coordinator.State.LastMessage));
    }

    [Fact]
    public async Task RequestNewCodeAsync_NotPairing_DoesNothing()
    {
        await _coordinator.RequestNewCodeAsync();

        Assert.Equal(ConnectionState.Initial, _coordinator.State);
    }

    [Fact]
    public async Task SubmitCodeAsync_Success_StoresPairingWithThumbprintAndToken()
    {
        var pairingClient = await BeginPairingAsync();
        SetupSuccess(pairingClient);
        EnqueueClient(status: Paired);

        await _coordinator.SubmitCodeAsync(Code, " " + DeviceName + " ");

        _store.Verify(s => s.SaveAsync(new StoredPairing(Host, DeviceId, DeviceName, TestSupport.Thumbprint, _time.GetUtcNow(), Token)), Times.Once);
    }

    [Fact]
    public async Task SubmitCodeAsync_Success_ReconnectsWithTokenAndIsConnected()
    {
        var pairingClient = await BeginPairingAsync();
        SetupSuccess(pairingClient);
        EnqueueClient(status: Paired);

        await _coordinator.SubmitCodeAsync(Code, DeviceName);
        await _coordinator.PairedLoop;

        pairingClient.Verify(c => c.DisposeAsync(), Times.Once);
        Assert.Equal((Token, TestSupport.Thumbprint), (_created[1].Token, _created[1].Trust.PinnedThumbprint));
        Assert.Equal(new ConnectionState(ConnectionStatus.PairedConnected, Host, DeviceName, ConnectionMessage.None), _coordinator.State);
    }

    [Theory]
    [InlineData(PairingOutcome.WrongCode, ConnectionMessage.WrongCode)]
    [InlineData(PairingOutcome.CodeExpired, ConnectionMessage.CodeExpired)]
    [InlineData(PairingOutcome.NoPendingCode, ConnectionMessage.NoPendingCode)]
    [InlineData(PairingOutcome.InvalidDeviceName, ConnectionMessage.DeviceNameRequired)]
    [InlineData(PairingOutcome.InvalidCodeFormat, ConnectionMessage.CodeFormat)]
    public async Task SubmitCodeAsync_Rejected_StaysAwaitingCodeWithMessage(PairingOutcome outcome, ConnectionMessage expected)
    {
        var client = await BeginPairingAsync();
        client.Setup(c => c.SubmitPairingCodeAsync(Code, DeviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PairingResultDto(outcome, null, null));

        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        Assert.Equal((ConnectionStatus.AwaitingCode, expected), (_coordinator.State.Status, _coordinator.State.LastMessage));
        _store.Verify(s => s.SaveAsync(It.IsAny<StoredPairing>()), Times.Never);
    }

    [Theory]
    [InlineData("12345", DeviceName, ConnectionMessage.CodeFormat)]
    [InlineData(null, DeviceName, ConnectionMessage.CodeFormat)]
    [InlineData(Code, "", ConnectionMessage.DeviceNameRequired)]
    [InlineData(Code, "   ", ConnectionMessage.DeviceNameRequired)]
    [InlineData(Code, null, ConnectionMessage.DeviceNameRequired)]
    [InlineData("abc", "", ConnectionMessage.DeviceNameRequired)]
    public async Task SubmitCodeAsync_InvalidInput_RejectedLocallyWithoutCallingService(string? code, string? name, ConnectionMessage expected)
    {
        var client = await BeginPairingAsync();

        await _coordinator.SubmitCodeAsync(code, name);

        client.Verify(c => c.SubmitPairingCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal((ConnectionStatus.AwaitingCode, expected), (_coordinator.State.Status, _coordinator.State.LastMessage));
    }

    [Fact]
    public async Task SubmitCodeAsync_CallFails_IsNotPairedWithError()
    {
        var client = await BeginPairingAsync();
        client.Setup(c => c.SubmitPairingCodeAsync(Code, DeviceName, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        Assert.Equal(ConnectionState.Initial with { Host = Host, LastMessage = ConnectionMessage.Unreachable }, _coordinator.State);
    }

    [Theory]
    [InlineData(null, Token)]
    [InlineData(DeviceId, null)]
    public async Task SubmitCodeAsync_SuccessWithoutIdOrToken_IsNotPairedWithError(string? deviceId, string? token)
    {
        var client = await BeginPairingAsync();
        client.Setup(c => c.SubmitPairingCodeAsync(Code, DeviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PairingResultDto(PairingOutcome.Success, deviceId, token));

        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        _store.Verify(s => s.SaveAsync(It.IsAny<StoredPairing>()), Times.Never);
        Assert.Equal(ConnectionMessage.Unreachable, _coordinator.State.LastMessage);
    }

    [Fact]
    public async Task SubmitCodeAsync_NoCertificateObserved_IsNotPairedWithError()
    {
        // This client never validates a certificate, so no thumbprint is observed.
        var client = NewClient();
        _factory
            .Setup(f => f.Create(It.IsAny<HostAddress>(), null, It.IsAny<CertificateTrustPolicy>()))
            .Returns(client.Object);
        await _coordinator.BeginPairingAsync(Host);
        SetupSuccess(client);

        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        _store.Verify(s => s.SaveAsync(It.IsAny<StoredPairing>()), Times.Never);
        Assert.Equal(ConnectionMessage.Unreachable, _coordinator.State.LastMessage);
    }

    [Fact]
    public async Task SubmitCodeAsync_NotPairing_DoesNothing()
    {
        await _coordinator.SubmitCodeAsync(Code, DeviceName);

        Assert.Equal(ConnectionState.Initial, _coordinator.State);
    }

    [Fact]
    public async Task CancelPairingAsync_AwaitingCode_IsNotPairedAndClosesConnection()
    {
        var client = await BeginPairingAsync();

        await _coordinator.CancelPairingAsync();

        client.Verify(c => c.DisposeAsync(), Times.Once);
        Assert.Equal(ConnectionState.Initial with { Host = Host }, _coordinator.State);
    }

    [Fact]
    public async Task CancelPairingAsync_NotPairing_DoesNothing()
    {
        await _coordinator.CancelPairingAsync();

        Assert.Equal(ConnectionState.Initial, _coordinator.State);
    }

    [Fact]
    public async Task PairingConnectionClosed_AwaitingCode_IsNotPairedWithError()
    {
        var client = await BeginPairingAsync();

        client.Raise(c => c.Closed += null);

        Assert.Equal(ConnectionState.Initial with { Host = Host, LastMessage = ConnectionMessage.Unreachable }, _coordinator.State);
    }

    [Fact]
    public async Task PairingConnectionClosed_AfterCancel_IsIgnored()
    {
        var client = await BeginPairingAsync();
        await _coordinator.CancelPairingAsync();

        client.Raise(c => c.Closed += null);

        Assert.Equal(ConnectionMessage.None, _coordinator.State.LastMessage);
    }

    // ---------- Remove pairing ----------

    [Fact]
    public async Task RemovePairingAsync_Connected_RemovesAtServiceAndLocally()
    {
        var client = await ConnectPairedAsync();

        var removed = await _coordinator.RemovePairingAsync();

        Assert.True(removed);
        client.Verify(c => c.RemovePairedDeviceAsync(DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.DisposeAsync(), Times.Once);
        _store.Verify(s => s.DeleteAsync(), Times.Once);
        Assert.Equal(ConnectionState.Initial, _coordinator.State);
    }

    [Fact]
    public async Task RemovePairingAsync_ServiceFails_KeepsPairingWithMessage()
    {
        var client = await ConnectPairedAsync();
        client.Setup(c => c.RemovePairedDeviceAsync(DeviceId, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        var removed = await _coordinator.RemovePairingAsync();

        Assert.False(removed);
        _store.Verify(s => s.DeleteAsync(), Times.Never);
        Assert.Equal((ConnectionStatus.PairedConnected, ConnectionMessage.RemoveFailed), (_coordinator.State.Status, _coordinator.State.LastMessage));
    }

    [Fact]
    public async Task RemovePairingAsync_PairedButDisconnected_ReturnsFalse()
    {
        var client = await ConnectPairedAsync();
        client.Raise(c => c.Reconnecting += null);

        var removed = await _coordinator.RemovePairingAsync();

        Assert.False(removed);
        client.Verify(c => c.RemovePairedDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemovePairingAsync_NotPaired_ReturnsFalse()
    {
        Assert.False(await _coordinator.RemovePairingAsync());
    }

    // ---------- General ----------

    [Fact]
    public async Task State_NeverContainsToken()
    {
        var states = RecordStates();
        await ConnectPairedAsync();

        Assert.All(states, s => Assert.DoesNotContain(Token, s.ToString()));
    }

    [Fact]
    public async Task DisposeAsync_WhileRetrying_StopsLoop()
    {
        StorePairing();
        EnqueueClient(startFails: true);
        await _coordinator.InitializeAsync();

        await _coordinator.DisposeAsync();

        await _coordinator.PairedLoop;
        Assert.Single(_created);
    }

    [Fact]
    public async Task DisposeAsync_WhilePairing_ClosesPairingConnection()
    {
        var client = await BeginPairingAsync();

        await _coordinator.DisposeAsync();

        client.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CancelledDuringConnect_EndsLoopWithoutRetry()
    {
        StorePairing();
        var start = new TaskCompletionSource();
        EnqueueClient(start: start.Task);
        await _coordinator.InitializeAsync();

        var dispose = _coordinator.DisposeAsync();
        start.SetException(new OperationCanceledException());
        await dispose;

        Assert.Single(_created);
    }

    // ---------- Helpers ----------

    private static Mock<IParentHubClient> NewClient()
    {
        var client = new Mock<IParentHubClient>();
        client.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(c => c.StartPairingAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NotPaired);
        client.Setup(c => c.RemovePairedDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        client.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return client;
    }

    private Mock<IParentHubClient> EnqueueClient(
        PairingStatusDto? status = null, bool startFails = false, bool statusFails = false, Task? start = null)
    {
        var client = NewClient();
        if (startFails)
        {
            client.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("unreachable"));
        }
        else if (start is not null)
        {
            client.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(start);
        }

        if (statusFails)
        {
            client.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        }
        else if (status is not null)
        {
            client.Setup(c => c.GetPairingStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        }

        _clients.Enqueue(client);
        return client;
    }

    private void StorePairing(string host = Host, string? thumbprint = null)
    {
        _store.Setup(s => s.LoadAsync()).ReturnsAsync(
            new StoredPairing(host, DeviceId, DeviceName, thumbprint ?? TestSupport.Thumbprint, DateTimeOffset.UnixEpoch, Token));
    }

    private async Task<Mock<IParentHubClient>> ConnectPairedAsync()
    {
        StorePairing();
        var client = EnqueueClient(status: Paired);
        await _coordinator.InitializeAsync();
        await _coordinator.PairedLoop;
        return client;
    }

    private async Task<Mock<IParentHubClient>> BeginPairingAsync()
    {
        var client = EnqueueClient();
        await _coordinator.BeginPairingAsync(Host);
        return client;
    }

    private static void SetupSuccess(Mock<IParentHubClient> client)
    {
        client.Setup(c => c.SubmitPairingCodeAsync(Code, DeviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PairingResultDto(PairingOutcome.Success, DeviceId, Token));
    }

    private List<ConnectionState> RecordStates()
    {
        var states = new List<ConnectionState>();
        _coordinator.StateChanged += states.Add;
        return states;
    }
}
