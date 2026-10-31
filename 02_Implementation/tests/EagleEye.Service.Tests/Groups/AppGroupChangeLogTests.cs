using EagleEye.Service.Communication;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Groups;
using EagleEye.Service.Rules;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class AppGroupChangeLogTests
{
    [Fact]
    public void Texts_AsInTheStory()
    {
        Assert.Equal("app group created: Games", AppGroupChangeLog.Created("Games"));
        Assert.Equal("app group renamed: Games → Gaming", AppGroupChangeLog.Renamed("Games", "Gaming"));
        Assert.Equal("app group deleted: Gaming", AppGroupChangeLog.Deleted("Gaming"));
        Assert.Equal("app moved: Minecraft Launcher → Games", AppGroupChangeLog.Moved("Minecraft Launcher", "Games"));
        Assert.Equal("app moved: Paint → not in any group", AppGroupChangeLog.Moved("Paint", null));
        Assert.Equal("group rule changed: Games, 1:00, Sa Su", AppGroupChangeLog.RuleChanged("Games", new LimitRule(60, BreakTimeDays.Saturday | BreakTimeDays.Sunday)));
        Assert.Throws<ArgumentNullException>(() => AppGroupChangeLog.RuleChanged("Games", null!));
    }

    [Fact]
    public async Task Broadcaster_GroupAreas_SentToParentsGroup()
    {
        var parents = new Mock<IParentClientCallback>();
        var clients = new Mock<IHubClients<IParentClientCallback>>();
        clients.Setup(c => c.Group(ParentHub.ParentsGroup)).Returns(parents.Object);
        var hub = new Mock<IHubContext<ParentHub, IParentClientCallback>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var groups = new AppGroupsDto(2, null, [], []);
        var rules = new AppGroupRulesDto(3, null, "S-1-5-21-1", []);
        var broadcaster = new AccountRulesBroadcaster(hub.Object);

        await broadcaster.BroadcastAsync(groups);
        await broadcaster.BroadcastAsync(rules);

        parents.Verify(p => p.OnAppGroupsChanged(groups), Times.Once);
        parents.Verify(p => p.OnAppGroupRulesChanged(rules), Times.Once);
        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.BroadcastAsync((AppGroupsDto)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.BroadcastAsync((AppGroupRulesDto)null!));
    }
}
