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
    private readonly Mock<ILogger<TrayHub>> _logger = new();

    [Fact]
    public async Task GetServiceVersion_ReturnsVersionFromProvider()
    {
        var expected = new ServiceVersionDto("EagleEye_v0.1");
        _versionProvider.Setup(p => p.GetVersion()).Returns(expected);
        var hub = CreateHub();

        var result = await hub.GetServiceVersion();

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task OnConnectedAsync_LogsConnectionEvent()
    {
        var hub = CreateHub();

        await hub.OnConnectedAsync();

        VerifyInformationLogged("Tray client connected: " + ConnectionId, exception: null);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutError_LogsDisconnectionEvent()
    {
        var hub = CreateHub();

        await hub.OnDisconnectedAsync(null);

        VerifyInformationLogged("Tray client disconnected: " + ConnectionId, exception: null);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithError_LogsDisconnectionEventWithException()
    {
        var hub = CreateHub();
        var error = new IOException("connection reset");

        await hub.OnDisconnectedAsync(error);

        VerifyInformationLogged("Tray client disconnected: " + ConnectionId, error);
    }

    private TrayHub CreateHub()
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(ConnectionId);
        return new TrayHub(_versionProvider.Object, _logger.Object) { Context = context.Object };
    }

    private void VerifyInformationLogged(string message, Exception? exception)
    {
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString() == message),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
