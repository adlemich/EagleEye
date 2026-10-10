using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Reports;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Reports;

public sealed class AccountUsageModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly ParentHubGateway _gateway = new(new FakeTimeProvider());
    private readonly Mock<IParentHubClient> _client = new();
    private readonly AccountUsageModel _model;

    public AccountUsageModelTests()
    {
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).ReturnsAsync(Usage(Kid, Day(Today, 1, 5), Day(Today.AddDays(-1), 1, 7)));
        _client.Setup(c => c.GetAccountUsageAsync(Kid2, It.IsAny<CancellationToken>())).ReturnsAsync(Usage(Kid2, Day(Today, 1, 9, Kid2)));
        _model = new AccountUsageModel(_gateway);
    }

    [Fact]
    public void New_NothingSelected_NotAvailable()
    {
        Assert.Equal((null, AccountsLoadState.NotAvailable, null, 0), (_model.SelectedAccountSid, _model.LoadState, _model.ServiceToday, _model.Days.Count));
    }

    [Fact]
    public async Task Select_Connected_LoadingThenReady()
    {
        var fetch = new TaskCompletionSource<AccountUsageDto>();
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).Returns(fetch.Task);
        _gateway.SetConnected(_client.Object);

        _model.SelectAccount(Kid);
        var during = _model.LoadState;
        fetch.SetResult(Usage(Kid, Day(Today, 1, 5)));
        await _model.LastFetch;

        Assert.Equal((AccountsLoadState.Loading, AccountsLoadState.Ready, (DateOnly?)Today), (during, _model.LoadState, _model.ServiceToday));
    }

    [Fact]
    public async Task Select_DaysNewestFirst()
    {
        await ConnectAndSelectAsync(Kid);

        Assert.Equal([Today, Today.AddDays(-1)], _model.Days.Select(d => d.Day));
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

        _client.Verify(c => c.GetAccountUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Select_None_ClearsAndNotAvailable()
    {
        await ConnectAndSelectAsync(Kid);

        _model.SelectAccount(null);

        Assert.Equal((AccountsLoadState.NotAvailable, 0), (_model.LoadState, _model.Days.Count));
    }

    [Fact]
    public async Task Select_OtherAccount_EarlierResultIgnored()
    {
        var slow = new TaskCompletionSource<AccountUsageDto>();
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).Returns(slow.Task);
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        var first = _model.LastFetch;

        _model.SelectAccount(Kid2);
        await _model.LastFetch;
        slow.SetResult(Usage(Kid, Day(Today, 1, 5)));
        await first;

        Assert.Equal(Kid2, _model.Days.Single().AccountSid);
    }

    [Fact]
    public async Task Connected_FetchesSelectedAccountAgain()
    {
        await ConnectAndSelectAsync(Kid);
        _gateway.SetDisconnected();

        _gateway.SetConnected(_client.Object);
        await _model.LastFetch;

        _client.Verify(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task Disconnected_NotAvailableAndForgetsDays()
    {
        await ConnectAndSelectAsync(Kid);
        var changed = false;
        _model.Changed += () => changed = true;

        _gateway.SetDisconnected();

        Assert.Equal((AccountsLoadState.NotAvailable, 0, null, true), (_model.LoadState, _model.Days.Count, _model.ServiceToday, changed));
    }

    [Fact]
    public async Task FetchFails_NotAvailable()
    {
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).ThrowsAsync(new HubException("The usage is not available."));
        _gateway.SetConnected(_client.Object);
        var states = new List<AccountsLoadState>();
        _model.Changed += () => states.Add(_model.LoadState);

        _model.SelectAccount(Kid);
        await _model.LastFetch;

        Assert.Equal([AccountsLoadState.Loading, AccountsLoadState.NotAvailable], states);
    }

    [Fact]
    public async Task FetchFailsAfterBroadcast_StaysReady()
    {
        var fetch = new TaskCompletionSource<AccountUsageDto>();
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).Returns(fetch.Task);
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        Broadcast(Day(Today, 3, 10));

        fetch.SetException(new IOException());
        await _model.LastFetch;

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task FetchFailureOfEarlierSelection_Ignored()
    {
        var slow = new TaskCompletionSource<AccountUsageDto>();
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).Returns(slow.Task);
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(Kid);
        var first = _model.LastFetch;
        _model.SelectAccount(Kid2);
        await _model.LastFetch;

        slow.SetException(new IOException());
        await first;

        Assert.Equal(AccountsLoadState.Ready, _model.LoadState);
    }

    [Fact]
    public async Task Broadcast_HigherRevisionAppliedPerDay()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(Day(Today, 2, 60));
        Broadcast(Day(Today, 1, 99));

        Assert.Equal(60, _model.Days.Single(d => d.Day == Today).Apps.Single().Seconds);
    }

    [Fact]
    public async Task Broadcast_OtherAccount_Ignored()
    {
        await ConnectAndSelectAsync(Kid);
        var changed = false;
        _model.Changed += () => changed = true;

        Broadcast(Day(Today, 9, 60, Kid2));

        Assert.False(changed);
    }

    [Fact]
    public async Task Broadcast_NewDay_Created()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(Day(Today.AddDays(-5), 2, 1));

        Assert.Equal(3, _model.Days.Count);
    }

    [Fact]
    public async Task Broadcast_Midnight_NewTodayAndEmptyPastDaysDropped()
    {
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).ReturnsAsync(Usage(Kid, new DayUsageDto(1, null, Kid, Today, Today, [])));
        await ConnectAndSelectAsync(Kid);

        Broadcast(new DayUsageDto(2, null, Kid, Today.AddDays(1), Today.AddDays(1), []));

        Assert.Equal(Today.AddDays(1), _model.ServiceToday);
        Assert.Equal([Today.AddDays(1)], _model.Days.Select(d => d.Day));
    }

    [Fact]
    public async Task Broadcast_OlderServiceToday_DoesNotMoveTodayBack()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(new DayUsageDto(5, null, Kid, Today.AddDays(-1), Today.AddDays(-1), [new AppUsageDto(1, "Editor", 9)]));

        Assert.Equal(Today, _model.ServiceToday);
    }

    [Fact]
    public async Task DaysOlderThan90_Dropped()
    {
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usage(Kid, Day(Today, 1, 1), Day(Today.AddDays(-89), 1, 1), Day(Today.AddDays(-90), 1, 1)));

        await ConnectAndSelectAsync(Kid);

        Assert.Equal([Today, Today.AddDays(-89)], _model.Days.Select(d => d.Day));
    }

    [Fact]
    public async Task Broadcast_RevisionZero_NotKept()
    {
        await ConnectAndSelectAsync(Kid);

        Broadcast(new DayUsageDto(0, null, Kid, Today.AddDays(-3), Today, [new AppUsageDto(1, "Editor", 9)]));

        Assert.DoesNotContain(_model.Days, d => d.Day == Today.AddDays(-3));
    }

    [Fact]
    public async Task ResultAndBroadcast_RaiseChanged()
    {
        _gateway.SetConnected(_client.Object);
        var changes = 0;
        _model.Changed += () => changes++;

        _model.SelectAccount(Kid);
        await _model.LastFetch;
        Broadcast(Day(Today, 2, 60));

        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task FetchFails_WithoutSubscriber_NotAvailable()
    {
        _client.Setup(c => c.GetAccountUsageAsync(Kid, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        var model = new AccountUsageModel(_gateway);
        _gateway.SetConnected(_client.Object);

        model.SelectAccount(Kid);
        await model.LastFetch;

        Assert.Equal(AccountsLoadState.NotAvailable, model.LoadState);
    }

    [Fact]
    public void Constructor_NullGateway_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AccountUsageModel(null!));
    }

    private async Task ConnectAndSelectAsync(string sid)
    {
        _gateway.SetConnected(_client.Object);
        _model.SelectAccount(sid);
        await _model.LastFetch;
    }

    private void Broadcast(DayUsageDto day) => _client.Raise(c => c.DayUsageChanged += null, day);

    private static DayUsageDto Day(DateOnly day, long revision, long seconds, string sid = Kid) =>
        new(revision, null, sid, day, Today, [new AppUsageDto(1, "Editor", seconds)]);

    private static AccountUsageDto Usage(string sid, params DayUsageDto[] days) => new(sid, Today, days);
}
