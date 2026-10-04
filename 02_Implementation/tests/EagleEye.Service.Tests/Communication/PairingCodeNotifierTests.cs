using EagleEye.Service.Communication;
using EagleEye.Service.Diagnostics;
using EagleEye.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class PairingCodeNotifierTests
{
    private const string Code = "012345";

    private readonly Mock<ITrayConnectionTracker> _tracker = new();
    private readonly Mock<IHubContext<TrayHub, ITrayClientCallback>> _hubContext = new();
    private readonly Mock<ITrayClientCallback> _allTrays = new();
    private readonly Mock<IPairingCodeEventLog> _eventLog = new();
    private readonly PairingCodeNotifier _notifier;

    public PairingCodeNotifierTests()
    {
        var clients = new Mock<IHubClients<ITrayClientCallback>>();
        clients.SetupGet(c => c.All).Returns(_allTrays.Object);
        _hubContext.SetupGet(h => h.Clients).Returns(clients.Object);
        _notifier = new PairingCodeNotifier(
            _tracker.Object, _hubContext.Object, _eventLog.Object, Mock.Of<ILogger<PairingCodeNotifier>>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task NotifyAsync_TrayConnected_PushesToAllTrays(int trayCount)
    {
        _tracker.SetupGet(t => t.Count).Returns(trayCount);

        await _notifier.NotifyAsync(Code);

        _allTrays.Verify(t => t.OnShowPairingCode(Code), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_TrayConnected_DoesNotWriteEventLog()
    {
        _tracker.SetupGet(t => t.Count).Returns(1);

        await _notifier.NotifyAsync(Code);

        _eventLog.Verify(e => e.Write(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_NoTrayConnected_WritesEventLog()
    {
        _tracker.SetupGet(t => t.Count).Returns(0);

        await _notifier.NotifyAsync(Code);

        _eventLog.Verify(e => e.Write(Code), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_NoTrayConnected_DoesNotPush()
    {
        _tracker.SetupGet(t => t.Count).Returns(0);

        await _notifier.NotifyAsync(Code);

        _allTrays.Verify(t => t.OnShowPairingCode(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NotifyAsync_MissingCode_ThrowsArgumentException(string? code)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _notifier.NotifyAsync(code!));
    }
}
