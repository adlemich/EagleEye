using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Accounts;

public sealed class UserAccountsModelTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly ParentHubGateway _gateway;
    private readonly Mock<IParentHubClient> _client = new();
    private readonly UserAccountsModel _model;

    public UserAccountsModelTests()
    {
        _gateway = new ParentHubGateway(_time);
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(1, false));
        _model = new UserAccountsModel(_gateway, _time);
    }

    // ---------- Load ----------

    [Fact]
    public void New_NotConnected_IsNotAvailable()
    {
        Assert.Equal((AccountsLoadState.NotAvailable, null), (_model.LoadState, _model.Snapshot));
    }

    [Fact]
    public async Task Connected_IsLoadingUntilFetchCompletes()
    {
        var fetch = new TaskCompletionSource<UserAccountListDto>();
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).Returns(fetch.Task);

        _gateway.SetConnected(_client.Object);
        var during = _model.LoadState;
        fetch.SetResult(Snapshot(1, true));
        await _model.LastFetch;

        Assert.Equal((AccountsLoadState.Loading, AccountsLoadState.Ready, 1L), (during, _model.LoadState, _model.Snapshot?.Revision));
    }

    [Fact]
    public async Task Connected_RaisesChangedForLoadingAndReady()
    {
        var count = 0;
        _model.Changed += () => count++;

        await ConnectAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Constructor_GatewayAlreadyConnected_Fetches()
    {
        await ConnectAsync();

        var model = new UserAccountsModel(_gateway, _time);
        await model.LastFetch;

        Assert.Equal(AccountsLoadState.Ready, model.LoadState);
    }

    [Fact]
    public async Task FetchFails_IsNotAvailable()
    {
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HubException("The accounts are not available."));

        await ConnectAsync();

        Assert.Equal(AccountsLoadState.NotAvailable, _model.LoadState);
    }

    [Fact]
    public async Task FetchFails_RaisesChanged()
    {
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        var states = new List<AccountsLoadState>();
        _model.Changed += () => states.Add(_model.LoadState);

        await ConnectAsync();

        Assert.Equal([AccountsLoadState.Loading, AccountsLoadState.NotAvailable], states);
    }

    [Fact]
    public async Task FetchFails_BroadcastAfterwards_IsReady()
    {
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        await ConnectAsync();

        Broadcast(Snapshot(2, true));

        Assert.Equal((AccountsLoadState.Ready, 2L), (_model.LoadState, _model.Snapshot?.Revision));
    }

    [Fact]
    public async Task FetchFailsAfterBroadcast_StaysReady()
    {
        var fetch = new TaskCompletionSource<UserAccountListDto>();
        _client.Setup(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).Returns(fetch.Task);
        _gateway.SetConnected(_client.Object);
        Broadcast(Snapshot(2, true));

        fetch.SetException(new IOException());
        await _model.LastFetch;

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task FetchResultOfEarlierConnection_IsIgnored()
    {
        var oldFetch = new TaskCompletionSource<UserAccountListDto>();
        var newFetch = new TaskCompletionSource<UserAccountListDto>();
        _client.SetupSequence(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).Returns(oldFetch.Task).Returns(newFetch.Task);
        _gateway.SetConnected(_client.Object);
        var oldTask = _model.LastFetch;
        _gateway.SetDisconnected();
        _gateway.SetConnected(_client.Object);

        oldFetch.SetResult(Snapshot(50, true));
        await oldTask;

        Assert.Equal((AccountsLoadState.Loading, null), (_model.LoadState, _model.Snapshot));
        newFetch.SetResult(Snapshot(1, false));
        await _model.LastFetch;
    }

    [Fact]
    public async Task FetchFailureOfEarlierConnection_DoesNotChangeState()
    {
        var oldFetch = new TaskCompletionSource<UserAccountListDto>();
        _client.SetupSequence(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>())).Returns(oldFetch.Task).ReturnsAsync(Snapshot(1, false));
        _gateway.SetConnected(_client.Object);
        var oldTask = _model.LastFetch;
        _gateway.SetDisconnected();
        _gateway.SetConnected(_client.Object);
        await _model.LastFetch;

        oldFetch.SetException(new IOException());
        await oldTask;

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task Disconnected_IsNotAvailableAndForgetsSnapshot()
    {
        await ConnectAsync();
        var changed = false;
        _model.Changed += () => changed = true;

        _gateway.SetDisconnected();

        Assert.Equal((AccountsLoadState.NotAvailable, null, true), (_model.LoadState, _model.Snapshot, changed));
    }

    [Fact]
    public async Task Reconnect_ResetsRevision_LowerRevisionIsApplied()
    {
        await ConnectAsync();
        Broadcast(Snapshot(9, true));
        _gateway.SetDisconnected();

        await ConnectAsync();

        Assert.Equal(1L, _model.Snapshot?.Revision);
    }

    [Fact]
    public async Task Broadcast_HigherRevision_IsAppliedAndRaisesChanged()
    {
        await ConnectAsync();
        var changed = false;
        _model.Changed += () => changed = true;

        Broadcast(Snapshot(2, true));

        Assert.Equal((true, true), (_model.Snapshot!.Accounts[0].IsUnderParentalControl, changed));
    }

    [Fact]
    public async Task Broadcast_Stale_IsIgnored()
    {
        await ConnectAsync();
        Broadcast(Snapshot(3, true));
        var changed = false;
        _model.Changed += () => changed = true;

        Broadcast(Snapshot(2, false));

        Assert.Equal((3L, false), (_model.Snapshot?.Revision, changed));
    }

    // ---------- Write ----------

    [Fact]
    public async Task Set_BroadcastWithOwnRequestIdBeforeAck_ReturnsTrue()
    {
        await ConnectAsync();
        SetupWrite((requestId, value) =>
        {
            Broadcast(Snapshot(2, value, requestId));
            return Task.FromResult(new StateWriteAckDto(2));
        });

        Assert.True(await _model.SetParentalControlAsync(Sid, true));
    }

    [Fact]
    public async Task Set_SendsSidAndValue()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(1)));

        await _model.SetParentalControlAsync(Sid, true);

        _client.Verify(c => c.SetParentalControlAsync(It.Is<Guid>(g => g != Guid.Empty), Sid, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_OwnBroadcastAfterAck_ReturnsTrue()
    {
        await ConnectAsync();
        Guid sent = default;
        SetupWrite((requestId, _) =>
        {
            sent = requestId;
            return Task.FromResult(new StateWriteAckDto(2));
        });

        var write = _model.SetParentalControlAsync(Sid, true);
        Broadcast(Snapshot(2, true, sent));

        Assert.True(await write.WaitAsync(Wait));
    }

    [Fact]
    public async Task Set_LaterForeignRevisionAfterAck_ReturnsTrue()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(3)));

        var write = _model.SetParentalControlAsync(Sid, true);
        Broadcast(Snapshot(4, false, Guid.NewGuid()));

        Assert.True(await write.WaitAsync(Wait));
    }

    [Fact]
    public async Task Set_ServiceOriginatedRevisionAfterAck_ReturnsTrue()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(3)));

        var write = _model.SetParentalControlAsync(Sid, true);
        Broadcast(Snapshot(4, true, requestId: null));

        Assert.True(await write.WaitAsync(Wait));
    }

    [Fact]
    public async Task Set_ForeignRevisionBelowAck_DoesNotConfirm()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(5)));

        var write = _model.SetParentalControlAsync(Sid, true);
        Broadcast(Snapshot(4, false, Guid.NewGuid()));
        await Task.Delay(20);

        Assert.False(write.IsCompleted);
        _time.Advance(UserAccountsModel.WriteTimeout);
        Assert.False(await write.WaitAsync(Wait));
    }

    [Fact]
    public async Task Set_AckRevisionAlreadyApplied_ReturnsTrueAtOnce()
    {
        await ConnectAsync();
        SetupWrite((_, _) =>
        {
            Broadcast(Snapshot(5, false, Guid.NewGuid()));
            return Task.FromResult(new StateWriteAckDto(4));
        });

        Assert.True(await _model.SetParentalControlAsync(Sid, true));
    }

    [Fact]
    public async Task Set_Rejected_ReturnsFalseWithoutRefetch()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromException<StateWriteAckDto>(new HubException("Unknown account.")));

        Assert.False(await _model.SetParentalControlAsync(Sid, true));
        _client.Verify(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_NoConfirmationWithinTimeout_ReturnsFalseAndRefetches()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(2)));

        var write = _model.SetParentalControlAsync(Sid, true);
        await Task.Delay(20);
        _time.Advance(UserAccountsModel.WriteTimeout);

        Assert.False(await write.WaitAsync(Wait));
        await _model.LastFetch;
        _client.Verify(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Set_CallHangs_TimesOutAfter4SecondsAndRefetches()
    {
        await ConnectAsync();
        _client.Setup(c => c.SetParentalControlAsync(It.IsAny<Guid>(), Sid, true, It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string _, bool _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new StateWriteAckDto(0);
            });

        var write = _model.SetParentalControlAsync(Sid, true);
        _time.Advance(UserAccountsModel.WriteTimeout - TimeSpan.FromMilliseconds(1));
        var before = write.IsCompleted;
        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(await write.WaitAsync(Wait));
        Assert.False(before);
        await _model.LastFetch;
        _client.Verify(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Set_ConnectionLostDuringCall_ReturnsFalseAndFetchesOnNextConnect()
    {
        await ConnectAsync();
        SetupWrite((_, _) =>
        {
            _gateway.SetDisconnected();
            return Task.FromException<StateWriteAckDto>(new IOException("connection lost"));
        });

        Assert.False(await _model.SetParentalControlAsync(Sid, true));
        await ConnectAsync();
        _client.Verify(c => c.GetUserAccountsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Set_DisconnectedWhileWaitingForConfirmation_ReturnsFalse()
    {
        await ConnectAsync();
        SetupWrite((_, _) => Task.FromResult(new StateWriteAckDto(2)));

        var write = _model.SetParentalControlAsync(Sid, true);
        await Task.Delay(20);
        _gateway.SetDisconnected();

        Assert.False(await write.WaitAsync(Wait));
    }

    [Fact]
    public async Task Set_NotConnected_ReturnsFalseAndNextConnectFetches()
    {
        Assert.False(await _model.SetParentalControlAsync(Sid, true));

        await ConnectAsync();

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task Set_InvalidSid_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _model.SetParentalControlAsync(" ", true));
    }

    [Fact]
    public void Constructor_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => new UserAccountsModel(null!, _time));
        Assert.Throws<ArgumentNullException>(() => new UserAccountsModel(_gateway, null!));
    }

    [Fact]
    public void Timeouts_Write4SecondsFetch15Seconds()
    {
        Assert.Equal((TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(15)), (UserAccountsModel.WriteTimeout, UserAccountsModel.FetchTimeout));
    }

    // ---------- Helpers ----------

    private async Task ConnectAsync()
    {
        _gateway.SetConnected(_client.Object);
        await _model.LastFetch;
    }

    private void Broadcast(UserAccountListDto snapshot) => _client.Raise(c => c.UserAccountsChanged += null, snapshot);

    private void SetupWrite(Func<Guid, bool, Task<StateWriteAckDto>> handler)
    {
        _client.Setup(c => c.SetParentalControlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((Guid requestId, string _, bool value, CancellationToken _) => handler(requestId, value));
    }

    private static UserAccountListDto Snapshot(long revision, bool ticked, Guid? requestId = null)
    {
        return new UserAccountListDto(revision, requestId, [new UserAccountDto(Sid, "max", "Max Adler", false, ticked)]);
    }
}
