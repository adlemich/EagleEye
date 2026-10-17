using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class KidMessengerTests
{
    private static readonly KidMessageDto Message = new(KidMessageReason.AppEarlyWarning, null, "Editor", 2, null, null);

    private readonly TrayConnectionRegistry _registry = new();
    private readonly Mock<ISingleClientProxy> _client = new();
    private readonly Mock<IHubContext<TrayHub>> _hub = new();
    private readonly FakeTimeProvider _time = new();
    private readonly TestLogger<KidMessenger> _logger = new();
    private readonly KidMessenger _messenger;

    public KidMessengerTests()
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Client("tray-2")).Returns(_client.Object);
        _hub.SetupGet(h => h.Clients).Returns(clients.Object);
        _messenger = new KidMessenger(_registry, _hub.Object, _time, _logger);
    }

    [Theory]
    [InlineData(KidMessageResult.Shown, KidMessageStates.Shown)]
    [InlineData(KidMessageResult.Replaced, KidMessageStates.Replaced)]
    [InlineData(KidMessageResult.AlreadyOpen, KidMessageStates.AlreadyOpen)]
    public async Task ShowAsync_Answer_MappedToState(KidMessageResult result, string expected)
    {
        Register();
        _client.Setup(c => c.InvokeCoreAsync<KidMessageResult>(nameof(ITrayClientCallback.ShowKidMessage),
                It.Is<object?[]>(a => ReferenceEquals(a[0], Message)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        Assert.Equal(expected, await _messenger.ShowAsync(2, Message));
    }

    [Fact]
    public async Task ShowAsync_NoVerifiedConnectionInTheSession_NotConnected()
    {
        _registry.Register("tray-3", new TrayClientIdentity(3, null, 1));

        Assert.Equal(KidMessageStates.NotConnected, await _messenger.ShowAsync(2, Message));
    }

    [Fact]
    public async Task ShowAsync_NoAnswerWithin3s_NoAnswer()
    {
        Register();
        _client.Setup(c => c.InvokeCoreAsync<KidMessageResult>(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns<string, object?[], CancellationToken>(async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return KidMessageResult.Shown;
            });

        var show = _messenger.ShowAsync(2, Message);
        _time.Advance(KidMessenger.AnswerTimeout);

        Assert.Equal(KidMessageStates.NoAnswer, await show);
    }

    [Fact]
    public async Task ShowAsync_CallFails_NoAnswerAndWarning()
    {
        Register();
        var failure = new HubException("Client didn't provide a result.");
        _client.Setup(c => c.InvokeCoreAsync<KidMessageResult>(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        Assert.Equal(KidMessageStates.NoAnswer, await _messenger.ShowAsync(2, Message));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public async Task ShowAsync_NullMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _messenger.ShowAsync(2, null!));
    }

    private void Register() => _registry.Register("tray-2", new TrayClientIdentity(2, "S-1-5-21-1-2-3-1003", 4242));
}
