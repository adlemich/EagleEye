using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Rules;

public sealed class AccountRulesModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly BreakTimeEntryDto Entry = new(3, false, 1200, 1439, BreakTimeDays.All);

    private readonly FakeTimeProvider _time = new();
    private readonly ParentHubGateway _gateway;
    private readonly Mock<IParentHubClient> _client = new();
    private readonly AccountRulesModel _model;

    public AccountRulesModelTests()
    {
        _gateway = new ParentHubGateway(_time);
        _client.Setup(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>())).ReturnsAsync(Rules(Kid, 5));
        _client.Setup(c => c.GetAccountRulesAsync(Kid2, It.IsAny<CancellationToken>())).ReturnsAsync(Rules(Kid2, 5));
        _model = new AccountRulesModel(_gateway, _time);
    }

    // ---------- Selection, fetch, broadcasts ----------

    [Fact]
    public void New_NotAvailable()
    {
        Assert.Equal((null, AccountsLoadState.NotAvailable, (AccountRulesDto?)null), (_model.SelectedAccountSid, _model.LoadState, _model.Snapshot));
    }

    [Fact]
    public async Task Select_Connected_LoadingThenReady()
    {
        var fetch = new TaskCompletionSource<AccountRulesDto>();
        _client.Setup(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>())).Returns(fetch.Task);
        _gateway.SetConnected(_client.Object);

        _model.SelectAccount(Kid);
        var during = _model.LoadState;
        fetch.SetResult(Rules(Kid, 5));
        await _model.LastFetch;

        Assert.Equal((AccountsLoadState.Loading, AccountsLoadState.Ready, 5L), (during, _model.LoadState, _model.Snapshot!.Revision));
    }

    [Fact]
    public void Select_NotConnected_NotAvailableWithoutFetch()
    {
        _model.SelectAccount(Kid);

        Assert.Equal((Kid, AccountsLoadState.NotAvailable), (_model.SelectedAccountSid, _model.LoadState));
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Select_SameAccountAgain_NoNewFetch()
    {
        await ConnectAndSelectAsync(Kid);

        _model.SelectAccount(Kid.ToLowerInvariant());

        _client.Verify(c => c.GetAccountRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Select_None_NotAvailable()
    {
        await ConnectAndSelectAsync(Kid);

        _model.SelectAccount(null);

        Assert.Equal((AccountsLoadState.NotAvailable, (AccountRulesDto?)null), (_model.LoadState, _model.Snapshot));
    }

    [Fact]
    public async Task Select_OtherAccountWhileFetching_EarlierResultIgnored()
    {
        var slow = new TaskCompletionSource<AccountRulesDto>();
        _client.Setup(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>())).Returns(slow.Task);
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        var first = _model.LastFetch;
        _model.SelectAccount(Kid2);
        await _model.LastFetch;

        slow.SetResult(Rules(Kid, 99));
        await first;

        Assert.Equal((Kid2, 5L), (_model.Snapshot!.AccountSid, _model.Snapshot.Revision));
    }

    [Fact]
    public async Task Reconnect_FetchesAgain()
    {
        await ConnectAndSelectAsync(Kid);

        _gateway.SetConnected(_client.Object);
        await _model.LastFetch;

        _client.Verify(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task FetchFails_NotAvailable()
    {
        _client.Setup(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>())).ThrowsAsync(new HubException("The rules are not available."));

        await ConnectAndSelectAsync(Kid);

        Assert.Equal(AccountsLoadState.NotAvailable, _model.LoadState);
    }

    [Fact]
    public async Task FetchFailsAfterTheSelectionChanged_Ignored()
    {
        var slow = new TaskCompletionSource<AccountRulesDto>();
        _client.Setup(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>())).Returns(slow.Task);
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        var first = _model.LastFetch;
        _model.SelectAccount(Kid2);
        await _model.LastFetch;

        slow.SetException(new HubException("x"));
        await first;

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task Disconnect_NotAvailable()
    {
        await ConnectAndSelectAsync(Kid);
        var changes = 0;
        _model.Changed += () => changes++;

        _gateway.SetDisconnected();

        Assert.Equal((AccountsLoadState.NotAvailable, (AccountRulesDto?)null, 1), (_model.LoadState, _model.Snapshot, changes));
    }

    [Fact]
    public async Task Broadcast_SelectedAccount_AppliedByRevision()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(Rules(Kid, 4) with { DisplayText = "old" });
        Broadcast(Rules(Kid, 6) with { DisplayText = "new" });

        Assert.Equal("new", _model.Snapshot!.DisplayText);
    }

    [Fact]
    public async Task Broadcast_OtherAccount_Ignored()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(Rules(Kid2, 9));

        Assert.Equal((Kid, 5L), (_model.Snapshot!.AccountSid, _model.Snapshot.Revision));
    }

    // ---------- Writes ----------

    [Fact]
    public async Task Write_ConfirmedByItsOwnBroadcastBeforeTheAck_True()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.SetBreakTimeEntryActiveAsync(It.IsAny<Guid>(), Kid, 3, true, It.IsAny<CancellationToken>()))
            .Returns<Guid, string, long, bool, CancellationToken>((id, _, _, _, _) =>
            {
                Broadcast(Rules(Kid, 6, id) with { BreakTimes = [Entry with { IsActive = true }] });
                return Task.FromResult(new StateWriteAckDto(6));
            });

        Assert.True(await _model.SetActiveAsync(3, true));
        Assert.True(_model.Snapshot!.BreakTimes.Single().IsActive);
    }

    [Fact]
    public async Task Write_AckFirstThenBroadcast_True()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.AddBreakTimeEntryAsync(It.IsAny<Guid>(), Kid, It.IsAny<CancellationToken>())).ReturnsAsync(new StateWriteAckDto(7));

        var write = _model.AddEntryAsync();
        Broadcast(Rules(Kid, 7));

        Assert.True(await write);
    }

    [Fact]
    public async Task Write_HubException_FalseWithoutRefetch()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.DeleteBreakTimeEntryAsync(It.IsAny<Guid>(), Kid, 3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HubException("The entry no longer exists."));

        Assert.False(await _model.DeleteEntryAsync(3));
        _client.Verify(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Write_TimeoutWithoutConfirmation_FalseAndRefetch()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.SetBreakTimeEntryTimeAsync(It.IsAny<Guid>(), Kid, 3, BreakTimeBoundary.End, 1260, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StateWriteAckDto(8));

        var write = _model.SetTimeAsync(3, BreakTimeBoundary.End, 1260);
        _time.Advance(AccountRulesModel.WriteTimeout);

        Assert.False(await write);
        await _model.LastFetch;
        _client.Verify(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Write_ConnectionLostDuringTheWrite_False()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.SetBreakTimeEntryDayAsync(It.IsAny<Guid>(), Kid, 3, DayOfWeek.Monday, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StateWriteAckDto(8));

        var write = _model.SetDayAsync(3, DayOfWeek.Monday, false);
        _gateway.SetDisconnected();

        Assert.False(await write);
    }

    [Fact]
    public async Task Write_AckAtOrBelowTheAppliedRevision_TrueAtOnce()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.SetDisplayTextAsync(It.IsAny<Guid>(), Kid, "Hallo", It.IsAny<CancellationToken>())).ReturnsAsync(new StateWriteAckDto(5));

        Assert.True(await _model.SetDisplayTextAsync("Hallo"));
    }

    [Fact]
    public async Task Write_NothingSelectedOrNotReady_False()
    {
        Assert.False(await _model.AddEntryAsync());
        _model.SelectAccount(Kid);
        Assert.False(await _model.AddEntryAsync());
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Write_GatewayNotConnected_False()
    {
        var gateway = new Mock<IParentHubGateway>();
        gateway.SetupGet(g => g.IsConnected).Returns(true);
        gateway.Setup(g => g.InvokeAsync(It.IsAny<Func<IParentHubClient, CancellationToken, Task<AccountRulesDto>>>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Rules(Kid, 5));
        gateway.Setup(g => g.InvokeAsync(It.IsAny<Func<IParentHubClient, CancellationToken, Task<StateWriteAckDto>>>(), It.IsAny<TimeSpan>()))
            .ThrowsAsync(new ParentHubNotConnectedException());
        var model = new AccountRulesModel(gateway.Object, _time);
        model.SelectAccount(Kid);
        await model.LastFetch;

        Assert.False(await model.AddEntryAsync());
        gateway.Verify(g => g.InvokeAsync(It.IsAny<Func<IParentHubClient, CancellationToken, Task<AccountRulesDto>>>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task Changed_RaisedOnSelectFetchAndFailure()
    {
        var changes = 0;
        _model.Changed += () => changes++;
        _client.Setup(c => c.GetAccountRulesAsync(Kid2, It.IsAny<CancellationToken>())).ThrowsAsync(new HubException("x"));
        _gateway.SetConnected(_client.Object);

        _model.SelectAccount(Kid);
        await _model.LastFetch;
        _model.SelectAccount(Kid2);
        await _model.LastFetch;

        Assert.Equal(5, changes); // connect, select, fetched, select, fetch failed
    }

    [Fact]
    public async Task Write_FailsAfterDisconnect_NoRefetch()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.AddBreakTimeEntryAsync(It.IsAny<Guid>(), Kid, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _gateway.SetDisconnected();
                return Task.FromException<StateWriteAckDto>(new IOException("lost"));
            });

        Assert.False(await _model.AddEntryAsync());
        _client.Verify(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Write_FailsAfterTheSelectionWasCleared_NoRefetch()
    {
        await ConnectAndSelectAsync(Kid);
        _client.Setup(c => c.AddBreakTimeEntryAsync(It.IsAny<Guid>(), Kid, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _model.SelectAccount(null);
                return Task.FromException<StateWriteAckDto>(new IOException("lost"));
            });

        Assert.False(await _model.AddEntryAsync());
        _client.Verify(c => c.GetAccountRulesAsync(Kid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new AccountRulesModel(null!, _time));
        Assert.Throws<ArgumentNullException>(() => new AccountRulesModel(_gateway, null!));
        Assert.Throws<ArgumentNullException>(() => { _ = _model.SetDisplayTextAsync(null!); });
    }

    private void Broadcast(AccountRulesDto snapshot) => _client.Raise(c => c.AccountRulesChanged += null, snapshot);

    private async Task ConnectAndSelectAsync(string sid)
    {
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(sid);
        await _model.LastFetch;
    }

    private static AccountRulesDto Rules(string sid, long revision, Guid? requestId = null) =>
        new(revision, requestId, sid, [Entry], "Text", false);
}
