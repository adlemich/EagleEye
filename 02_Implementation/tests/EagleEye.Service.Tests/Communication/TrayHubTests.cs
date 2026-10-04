using System.Net;
using EagleEye.Service.Communication;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class TrayHubTests
{
    private const string ConnectionId = "connection-1";

    private readonly Mock<IVersionProvider> _versionProvider = new();
    private readonly Mock<ITrayConnectionTracker> _tracker = new();
    private readonly Mock<ILogger<TrayHub>> _logger = new();

    [Fact]
    public async Task GetServiceVersion_ReturnsVersionFromProvider()
    {
        var expected = new ServiceVersionDto("EagleEye_v0.1");
        _versionProvider.Setup(p => p.GetVersion()).Returns(expected);
        var (hub, _) = CreateHub(IPAddress.Loopback);

        var result = await hub.GetServiceVersion();

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task OnConnectedAsync_LogsConnectionEvent()
    {
        var (hub, _) = CreateHub(IPAddress.Loopback);

        await hub.OnConnectedAsync();

        VerifyLogged(LogLevel.Information, "Tray client connected: " + ConnectionId, exception: null);
    }

    [Theory]
    [MemberData(nameof(LoopbackAddresses))]
    public async Task OnConnectedAsync_LoopbackAddress_IncrementsTracker(IPAddress address)
    {
        var (hub, _) = CreateHub(address);

        await hub.OnConnectedAsync();

        _tracker.Verify(t => t.Increment(), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_RemoteAddress_AbortsWithoutTracking()
    {
        var (hub, context) = CreateHub(IPAddress.Parse("192.168.1.20"));

        await hub.OnConnectedAsync();

        context.Verify(c => c.Abort(), Times.Once);
        _tracker.Verify(t => t.Increment(), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_NoRemoteAddress_Aborts()
    {
        var (hub, context) = CreateHub(remoteAddress: null);

        await hub.OnConnectedAsync();

        context.Verify(c => c.Abort(), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_NoHttpContext_Aborts()
    {
        var context = HubContextFactory.Create(ConnectionId, withHttpContext: false);
        var hub = new TrayHub(_versionProvider.Object, _tracker.Object, _logger.Object) { Context = context.Object };

        await hub.OnConnectedAsync();

        context.Verify(c => c.Abort(), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_RemoteAddress_LogsWarning()
    {
        var (hub, _) = CreateHub(IPAddress.Parse("10.0.0.5"));

        await hub.OnConnectedAsync();

        VerifyLogged(LogLevel.Warning, "Tray connection from a non-loopback address rejected: " + ConnectionId, exception: null);
    }

    [Fact]
    public async Task OnDisconnectedAsync_TrackedConnection_DecrementsTracker()
    {
        var (hub, _) = CreateHub(IPAddress.Loopback);
        await hub.OnConnectedAsync();

        await hub.OnDisconnectedAsync(null);

        _tracker.Verify(t => t.Decrement(), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_RejectedConnection_DoesNotDecrementTracker()
    {
        var (hub, _) = CreateHub(IPAddress.Parse("192.168.1.20"));
        await hub.OnConnectedAsync();

        await hub.OnDisconnectedAsync(null);

        _tracker.Verify(t => t.Decrement(), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutError_LogsDisconnectionEvent()
    {
        var (hub, _) = CreateHub(IPAddress.Loopback);

        await hub.OnDisconnectedAsync(null);

        VerifyLogged(LogLevel.Information, "Tray client disconnected: " + ConnectionId, exception: null);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithError_LogsDisconnectionEventWithException()
    {
        var (hub, _) = CreateHub(IPAddress.Loopback);
        var error = new IOException("connection reset");

        await hub.OnDisconnectedAsync(error);

        VerifyLogged(LogLevel.Information, "Tray client disconnected: " + ConnectionId, error);
    }

    public static TheoryData<IPAddress> LoopbackAddresses => new() { IPAddress.Loopback, IPAddress.IPv6Loopback };

    private (TrayHub Hub, Mock<HubCallerContext> Context) CreateHub(IPAddress? remoteAddress)
    {
        var context = HubContextFactory.Create(ConnectionId, remoteAddress);
        var hub = new TrayHub(_versionProvider.Object, _tracker.Object, _logger.Object) { Context = context.Object };
        return (hub, context);
    }

    private void VerifyLogged(LogLevel level, string message, Exception? exception)
    {
        _logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString() == message),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
