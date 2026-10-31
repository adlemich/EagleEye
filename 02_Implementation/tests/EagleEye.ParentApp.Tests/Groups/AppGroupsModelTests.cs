using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Groups;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Groups;

/// <summary>US-008 models; the shared replica and write logic is covered by <c>AccountRulesModelTests</c>.</summary>
public sealed class AppGroupsModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";

    private readonly FakeTimeProvider _time = new();
    private readonly ParentHubGateway _gateway;
    private readonly Mock<IParentHubClient> _client = new();

    public AppGroupsModelTests()
    {
        _gateway = new ParentHubGateway(_time);
        _client.Setup(c => c.GetAppGroupsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Groups(2));
        _client.Setup(c => c.GetAppGroupRulesAsync(Kid, It.IsAny<CancellationToken>())).ReturnsAsync(Rules(Kid, 5));
    }

    [Fact]
    public async Task AppGroups_FetchedOnConnect_BroadcastApplied()
    {
        var model = new AppGroupsModel(_gateway, _time);
        Assert.Equal(AccountsLoadState.NotAvailable, model.LoadState);

        _gateway.SetConnected(_client.Object);
        await model.LastFetch;
        Assert.Equal((AccountsLoadState.Ready, 2L), (model.LoadState, model.Snapshot!.Revision));

        _client.Raise(c => c.AppGroupsChanged += null, Groups(4));
        Assert.Equal(4, model.Snapshot!.Revision);
    }

    [Fact]
    public async Task AppGroups_Writes_DelegatedAndConfirmed()
    {
        var model = new AppGroupsModel(_gateway, _time);
        _gateway.SetConnected(_client.Object);
        await model.LastFetch;
        Confirm(c => c.CreateAppGroupAsync(It.IsAny<Guid>(), "Games", It.IsAny<CancellationToken>()), 3);
        Confirm(c => c.RenameAppGroupAsync(It.IsAny<Guid>(), 7, "Gaming", It.IsAny<CancellationToken>()), 4);
        Confirm(c => c.MoveAppToGroupAsync(It.IsAny<Guid>(), "p", 7, It.IsAny<CancellationToken>()), 5);
        Confirm(c => c.MoveAppToGroupAsync(It.IsAny<Guid>(), "p", null, It.IsAny<CancellationToken>()), 6);
        Confirm(c => c.DeleteAppGroupAsync(It.IsAny<Guid>(), 7, It.IsAny<CancellationToken>()), 7);

        Assert.True(await model.CreateAsync("Games"));
        Assert.True(await model.RenameAsync(7, "Gaming"));
        Assert.True(await model.MoveAsync("p", 7));
        Assert.True(await model.MoveAsync("p", null));
        Assert.True(await model.DeleteAsync(7));
        Assert.Equal(7, model.Snapshot!.Revision);
    }

    [Fact]
    public async Task AppGroups_WriteRejected_False()
    {
        var model = new AppGroupsModel(_gateway, _time);
        _gateway.SetConnected(_client.Object);
        await model.LastFetch;
        _client.Setup(c => c.CreateAppGroupAsync(It.IsAny<Guid>(), "Games", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HubException("A group with this name already exists."));

        Assert.False(await model.CreateAsync("Games"));
    }

    [Fact]
    public async Task AppGroups_NullArguments_Throw()
    {
        var model = new AppGroupsModel(_gateway, _time);

        await Assert.ThrowsAsync<ArgumentNullException>(() => model.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => model.RenameAsync(7, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => model.MoveAsync(null!, 7));
    }

    [Fact]
    public async Task GroupRules_SelectedAccountFetched_OthersIgnored_WritesDelegated()
    {
        var model = new AppGroupRulesModel(_gateway, _time);
        _gateway.SetConnected(_client.Object);
        model.SelectAccount(Kid);
        await model.LastFetch;
        Assert.Equal((AccountsLoadState.Ready, 5L), (model.LoadState, model.Snapshot!.Revision));

        _client.Raise(c => c.AppGroupRulesChanged += null, Rules(Kid2, 9));
        Assert.Equal(5, model.Snapshot!.Revision);

        _client.Setup(c => c.SetAppGroupBudgetAsync(It.IsAny<Guid>(), Kid, 7, AppBudgetPart.Minutes, 30, It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.AppGroupRulesChanged += null, Rules(Kid, 6)))
            .ReturnsAsync(new StateWriteAckDto(6));
        _client.Setup(c => c.SetAppGroupDayAsync(It.IsAny<Guid>(), Kid, 7, DayOfWeek.Sunday, true, It.IsAny<CancellationToken>()))
            .Callback(() => _client.Raise(c => c.AppGroupRulesChanged += null, Rules(Kid, 7)))
            .ReturnsAsync(new StateWriteAckDto(7));
        Assert.True(await model.SetBudgetAsync(7, AppBudgetPart.Minutes, 30));
        Assert.True(await model.SetDayAsync(7, DayOfWeek.Sunday, true));
    }

    private void Confirm(System.Linq.Expressions.Expression<Func<IParentHubClient, Task<StateWriteAckDto>>> call, long revision) =>
        _client.Setup(call)
            .Callback(() => _client.Raise(c => c.AppGroupsChanged += null, Groups(revision)))
            .ReturnsAsync(new StateWriteAckDto(revision));

    private static AppGroupsDto Groups(long revision) => new(revision, null, [new AppGroupDto(7, "Games")], [new GroupableAppDto("p", "Paint", 7)]);

    private static AppGroupRulesDto Rules(string sid, long revision) => new(revision, null, sid, [new AppGroupRuleDto(7, 60, BreakTimeDays.All)]);
}
