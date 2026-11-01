using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class OverlayPublisherTests
{
    private static readonly RemainingTimeDto Model = new([new OverlayLineDto(OverlayLineKind.App, "Editor", 120, OverlayLevel.Early, true)], 5, 1, OverlaySignal.Early);

    private readonly TrayConnectionRegistry _registry = new();
    private readonly Mock<ISingleClientProxy> _client = new();
    private readonly Mock<IHubClients> _clients = new();
    private readonly Mock<IHubContext<TrayHub>> _hub = new();
    private readonly TestLogger<OverlayPublisher> _logger = new();
    private readonly OverlayPublisher _publisher;

    public OverlayPublisherTests()
    {
        _clients.Setup(c => c.Client("tray-2")).Returns(_client.Object);
        _hub.SetupGet(h => h.Clients).Returns(_clients.Object);
        _client.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _publisher = new OverlayPublisher(_registry, _hub.Object, _logger);
    }

    [Fact]
    public void Publish_NoConnection_NotConnected_NothingSent()
    {
        Assert.Equal(KidMessageStates.NotConnected, _publisher.Publish(2, Model));

        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public void Publish_NewestVerifiedConnection_ShownInOverlay_OneSend()
    {
        _registry.Register("tray-1", new TrayClientIdentity(2, "S-1", 1));
        _registry.Register("tray-2", new TrayClientIdentity(2, "S-1", 2));

        Assert.Equal(KidMessageStates.ShownInOverlay, _publisher.Publish(2, Model));

        _client.Verify(
            c => c.SendCoreAsync(nameof(ITrayClientCallback.OnRemainingTime), It.Is<object?[]>(a => ReferenceEquals(a[0], Model)), It.IsAny<CancellationToken>()),
            Times.Once);
        _clients.Verify(c => c.Client("tray-1"), Times.Never);
    }

    [Fact]
    public void Publish_SendFails_NeverThrows_DebugLogged()
    {
        var failure = new InvalidOperationException("gone");
        _client.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        Register();

        Assert.Equal(KidMessageStates.ShownInOverlay, _publisher.Publish(2, Model));
        Assert.True(_logger.Has(LogLevel.Debug, failure));
    }

    [Fact]
    public void Publish_ClientLookupFails_NeverThrows_DebugLogged()
    {
        var failure = new InvalidOperationException("hub");
        _clients.Setup(c => c.Client("tray-2")).Throws(failure);
        Register();

        Assert.Equal(KidMessageStates.ShownInOverlay, _publisher.Publish(2, Model));
        Assert.True(_logger.Has(LogLevel.Debug, failure));
    }

    [Fact]
    public void Publish_NullModel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _publisher.Publish(2, null!));
    }

    private void Register() => _registry.Register("tray-2", new TrayClientIdentity(2, "S-1", 4242));
}
