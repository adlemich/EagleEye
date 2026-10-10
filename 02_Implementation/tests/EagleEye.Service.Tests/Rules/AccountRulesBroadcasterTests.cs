using EagleEye.Service.Communication;
using EagleEye.Service.Rules;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class AccountRulesBroadcasterTests
{
    [Fact]
    public async Task BroadcastAsync_SendsSnapshotToParentsGroup()
    {
        var parents = new Mock<IParentClientCallback>();
        var clients = new Mock<IHubClients<IParentClientCallback>>();
        clients.Setup(c => c.Group(ParentHub.ParentsGroup)).Returns(parents.Object);
        var hub = new Mock<IHubContext<ParentHub, IParentClientCallback>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var snapshot = new AccountRulesDto(2, null, "S-1-5-21-1-2-3-1003", [], "x", false);

        await new AccountRulesBroadcaster(hub.Object).BroadcastAsync(snapshot);

        parents.Verify(p => p.OnAccountRulesChanged(snapshot), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_Null_ThrowsArgumentNullException()
    {
        var broadcaster = new AccountRulesBroadcaster(Mock.Of<IHubContext<ParentHub, IParentClientCallback>>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.BroadcastAsync(null!));
    }
}
