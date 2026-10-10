using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Pairing;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class ParentHubTests
{
    private const string ConnectionId = "connection-1";
    private const string DeviceId = "6f1f8a0e-4c2b-4bd8-9d5e-0b6f1b2c3d4e";
    private const string DeviceName = "Dad's laptop";

    private static readonly PairedDevice Device = new(DeviceId, DeviceName, new byte[32], DateTimeOffset.UnixEpoch);

    private readonly Mock<IPairingManager> _pairing = new();
    private readonly Mock<IParentConnectionRegistry> _registry = new();
    private readonly Mock<IGroupManager> _groups = new();
    private readonly Mock<IUserAccountService> _userAccounts = new();
    private readonly Mock<ILogger<ParentHub>> _logger = new();

    [Fact]
    public async Task OnConnectedAsync_KnownToken_MarksConnectionPaired()
    {
        _pairing.Setup(p => p.AuthenticateAsync("token-1")).ReturnsAsync(Device);
        var (hub, context) = CreateHub("Bearer token-1");

        await hub.OnConnectedAsync();

        Assert.Equal(DeviceId, ParentConnectionState.GetDeviceId(context.Object));
    }

    [Fact]
    public async Task OnConnectedAsync_KnownToken_AddsToParentsGroupAndRegisters()
    {
        _pairing.Setup(p => p.AuthenticateAsync("token-1")).ReturnsAsync(Device);
        var (hub, context) = CreateHub("Bearer token-1");

        await hub.OnConnectedAsync();

        _groups.Verify(g => g.AddToGroupAsync(ConnectionId, ParentHub.ParentsGroup, It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.Register(DeviceId, context.Object), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_UnknownToken_StaysUnpaired()
    {
        var (hub, context) = CreateHub("Bearer unknown");

        await hub.OnConnectedAsync();

        Assert.False(ParentConnectionState.IsPaired(context.Object));
        _groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_NoAuthorizationHeader_AuthenticatesWithNullToken()
    {
        var (hub, _) = CreateHub(authorization: null);

        await hub.OnConnectedAsync();

        _pairing.Verify(p => p.AuthenticateAsync(null), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_NoHttpContext_AuthenticatesWithNullToken()
    {
        var context = HubContextFactory.Create(ConnectionId, withHttpContext: false);
        var hub = new ParentHub(_pairing.Object, _registry.Object, _userAccounts.Object, Mock.Of<IUsageService>(), _logger.Object) { Context = context.Object, Groups = _groups.Object };

        await hub.OnConnectedAsync();

        _pairing.Verify(p => p.AuthenticateAsync(null), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_PairedConnection_Unregisters()
    {
        var (hub, context) = CreateHub();
        ParentConnectionState.SetPaired(context.Object, DeviceId, DeviceName);

        await hub.OnDisconnectedAsync(null);

        _registry.Verify(r => r.Unregister(DeviceId, ConnectionId), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_UnpairedConnection_DoesNotUnregister()
    {
        var (hub, _) = CreateHub();

        await hub.OnDisconnectedAsync(new IOException("reset"));

        _registry.Verify(r => r.Unregister(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetPairingStatus_Unpaired_ReturnsNotPaired()
    {
        var (hub, _) = CreateHub();

        var status = await hub.GetPairingStatus();

        Assert.Equal(new PairingStatusDto(false, null, null), status);
    }

    [Fact]
    public async Task GetPairingStatus_Paired_ReturnsDevice()
    {
        var (hub, context) = CreateHub();
        ParentConnectionState.SetPaired(context.Object, DeviceId, DeviceName);

        var status = await hub.GetPairingStatus();

        Assert.Equal(new PairingStatusDto(true, DeviceId, DeviceName), status);
    }

    [Fact]
    public async Task StartPairing_DelegatesWithConnectionId()
    {
        var (hub, _) = CreateHub();

        await hub.StartPairing();

        _pairing.Verify(p => p.StartPairingAsync(ConnectionId), Times.Once);
    }

    [Fact]
    public async Task StartPairing_ManagerFails_ThrowsSafeHubException()
    {
        _pairing.Setup(p => p.StartPairingAsync(ConnectionId)).ThrowsAsync(new InvalidOperationException("C:\\secret\\path"));
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.StartPairing());

        Assert.Equal(ParentHub.StartFailedMessage, ex.Message);
    }

    [Fact]
    public async Task StartPairing_HubException_IsPassedThrough()
    {
        var original = new HubException("as is");
        _pairing.Setup(p => p.StartPairingAsync(ConnectionId)).ThrowsAsync(original);
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.StartPairing());

        Assert.Same(original, ex);
    }

    [Fact]
    public async Task SubmitPairingCode_ReturnsManagerResult()
    {
        var expected = new PairingResultDto(PairingOutcome.WrongCode, null, null);
        _pairing.Setup(p => p.SubmitAsync(ConnectionId, "123456", DeviceName)).ReturnsAsync(expected);
        var (hub, _) = CreateHub();

        var result = await hub.SubmitPairingCode("123456", DeviceName);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task SubmitPairingCode_ManagerFails_ThrowsSafeHubException()
    {
        _pairing.Setup(p => p.SubmitAsync(ConnectionId, It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("SQL error"));
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SubmitPairingCode("123456", DeviceName));

        Assert.Equal(ParentHub.SubmitFailedMessage, ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RemovePairedDevice_EmptyId_ThrowsUnknownDevice(string deviceId)
    {
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RemovePairedDevice(deviceId));

        Assert.Equal(ParentHub.UnknownDeviceMessage, ex.Message);
    }

    [Fact]
    public async Task RemovePairedDevice_UnknownDevice_ThrowsUnknownDevice()
    {
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RemovePairedDevice("other"));

        Assert.Equal(ParentHub.UnknownDeviceMessage, ex.Message);
    }

    [Fact]
    public async Task RemovePairedDevice_ManagerFails_ThrowsSafeHubException()
    {
        _pairing.Setup(p => p.RemoveDeviceAsync(DeviceId)).ThrowsAsync(new InvalidOperationException("db"));
        var (hub, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RemovePairedDevice(DeviceId));

        Assert.Equal(ParentHub.RemoveFailedMessage, ex.Message);
    }

    [Fact]
    public async Task RemovePairedDevice_OwnDevice_ClearsPairingAndLeavesGroup()
    {
        _pairing.Setup(p => p.RemoveDeviceAsync(DeviceId)).ReturnsAsync(true);
        var (hub, context) = CreateHub();
        ParentConnectionState.SetPaired(context.Object, DeviceId, DeviceName);

        await hub.RemovePairedDevice(DeviceId);

        Assert.False(ParentConnectionState.IsPaired(context.Object));
        _groups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, ParentHub.ParentsGroup, It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.Unregister(DeviceId, ConnectionId), Times.Once);
    }

    [Fact]
    public async Task RemovePairedDevice_AnyDevice_AbortsOtherConnectionsOfThatDevice()
    {
        _pairing.Setup(p => p.RemoveDeviceAsync(DeviceId)).ReturnsAsync(true);
        var (hub, context) = CreateHub();
        ParentConnectionState.SetPaired(context.Object, DeviceId, DeviceName);

        await hub.RemovePairedDevice(DeviceId);

        _registry.Verify(r => r.AbortAll(DeviceId, ConnectionId), Times.Once);
    }

    [Fact]
    public async Task RemovePairedDevice_OtherDevice_KeepsCallerPaired()
    {
        _pairing.Setup(p => p.RemoveDeviceAsync("other")).ReturnsAsync(true);
        var (hub, context) = CreateHub();
        ParentConnectionState.SetPaired(context.Object, DeviceId, DeviceName);

        await hub.RemovePairedDevice("other");

        Assert.True(ParentConnectionState.IsPaired(context.Object));
        _groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private (ParentHub Hub, Mock<HubCallerContext> Context) CreateHub(string? authorization = null)
    {
        var context = HubContextFactory.Create(ConnectionId, authorization: authorization);
        var hub = new ParentHub(_pairing.Object, _registry.Object, _userAccounts.Object, Mock.Of<IUsageService>(), _logger.Object)
        {
            Context = context.Object,
            Groups = _groups.Object,
        };
        return (hub, context);
    }
}
