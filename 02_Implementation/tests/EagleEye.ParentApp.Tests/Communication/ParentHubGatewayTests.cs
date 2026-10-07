using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentHubGatewayTests
{
    private static readonly UserAccountListDto Snapshot = new(5, null, []);

    private readonly FakeTimeProvider _time = new();
    private readonly ParentHubGateway _gateway;
    private readonly Mock<IParentHubClient> _client = new();

    public ParentHubGatewayTests()
    {
        _gateway = new ParentHubGateway(_time);
    }

    [Fact]
    public async Task InvokeAsync_NotConnected_ThrowsNotConnected()
    {
        await Assert.ThrowsAsync<ParentHubNotConnectedException>(
            () => _gateway.InvokeAsync((c, ct) => c.GetUserAccountsAsync(ct), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task InvokeAsync_AfterDisconnect_ThrowsNotConnected()
    {
        _gateway.SetConnected(_client.Object);
        _gateway.SetDisconnected();

        await Assert.ThrowsAsync<ParentHubNotConnectedException>(
            () => _gateway.InvokeAsync((c, ct) => c.GetUserAccountsAsync(ct), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task InvokeAsync_Connected_RunsCallOnCurrentClient()
    {
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot);
        _gateway.SetConnected(_client.Object);

        Assert.Same(Snapshot, await _gateway.InvokeAsync((c, ct) => c.GetUserAccountsAsync(ct), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task InvokeAsync_Connected_TokenCancelsAfterTimeout()
    {
        _gateway.SetConnected(_client.Object);
        CancellationToken observed = default;
        var call = _gateway.InvokeAsync(async (_, ct) =>
        {
            observed = ct;
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }, TimeSpan.FromSeconds(4));

        _time.Advance(TimeSpan.FromSeconds(3.9));
        var before = observed.IsCancellationRequested;
        _time.Advance(TimeSpan.FromSeconds(0.1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.False(before);
    }

    [Fact]
    public async Task InvokeAsync_NullCall_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _gateway.InvokeAsync<int>(null!, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SetConnected_RaisesConnectedEveryTime()
    {
        var count = 0;
        _gateway.Connected += () => count++;

        _gateway.SetConnected(_client.Object);
        _gateway.SetDisconnected();
        _gateway.SetConnected(_client.Object);

        Assert.Equal((2, true), (count, _gateway.IsConnected));
    }

    [Fact]
    public void SetDisconnected_WhenConnected_RaisesOnce()
    {
        var count = 0;
        _gateway.Disconnected += () => count++;
        _gateway.SetConnected(_client.Object);

        _gateway.SetDisconnected();
        _gateway.SetDisconnected();

        Assert.Equal((1, false), (count, _gateway.IsConnected));
    }

    [Fact]
    public void SetDisconnected_NeverConnected_DoesNotRaise()
    {
        var raised = false;
        _gateway.Disconnected += () => raised = true;

        _gateway.SetDisconnected();

        Assert.False(raised);
    }

    [Fact]
    public void Events_WithoutHandlers_DoNotThrow()
    {
        _gateway.SetConnected(_client.Object);
        _client.Raise(c => c.UserAccountsChanged += null, Snapshot);
        _gateway.SetDisconnected();

        Assert.False(_gateway.IsConnected);
    }

    [Fact]
    public void UserAccountsChanged_FromConnectedClient_IsForwarded()
    {
        var received = RecordSnapshots();
        _gateway.SetConnected(_client.Object);

        _client.Raise(c => c.UserAccountsChanged += null, Snapshot);

        Assert.Same(Snapshot, Assert.Single(received));
    }

    [Fact]
    public void UserAccountsChanged_WhileDisconnected_IsNotForwarded()
    {
        var received = RecordSnapshots();
        _gateway.SetConnected(_client.Object);
        _gateway.SetDisconnected();

        _client.Raise(c => c.UserAccountsChanged += null, Snapshot);

        Assert.Empty(received);
    }

    [Fact]
    public void SwitchingClients_UnsubscribesTheOldOne()
    {
        var received = RecordSnapshots();
        var next = new Mock<IParentHubClient>();
        _gateway.SetConnected(_client.Object);
        _gateway.SetDisconnected();
        _gateway.SetConnected(next.Object);

        _client.Raise(c => c.UserAccountsChanged += null, Snapshot);
        next.Raise(c => c.UserAccountsChanged += null, Snapshot);

        Assert.Single(received);
        _client.VerifyRemove(c => c.UserAccountsChanged -= It.IsAny<Action<UserAccountListDto>>(), Times.Once);
    }

    [Fact]
    public void SetConnected_SameClientTwice_SubscribesOnce()
    {
        var received = RecordSnapshots();
        _gateway.SetConnected(_client.Object);
        _gateway.SetDisconnected();
        _gateway.SetConnected(_client.Object);

        _client.Raise(c => c.UserAccountsChanged += null, Snapshot);

        Assert.Single(received);
        _client.VerifyAdd(c => c.UserAccountsChanged += It.IsAny<Action<UserAccountListDto>>(), Times.Once);
    }

    [Fact]
    public void SetConnected_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gateway.SetConnected(null!));
    }

    private List<UserAccountListDto> RecordSnapshots()
    {
        var received = new List<UserAccountListDto>();
        _gateway.UserAccountsChanged += received.Add;
        return received;
    }
}
