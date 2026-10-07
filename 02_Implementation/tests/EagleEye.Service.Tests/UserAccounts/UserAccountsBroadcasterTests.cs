using EagleEye.Service.Communication;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.UserAccounts;

public sealed class UserAccountsBroadcasterTests
{
    [Fact]
    public async Task BroadcastAsync_SendsSnapshotToParentsGroup()
    {
        var parents = new Mock<IParentClientCallback>();
        var clients = new Mock<IHubClients<IParentClientCallback>>();
        clients.Setup(c => c.Group(ParentHub.ParentsGroup)).Returns(parents.Object);
        var hub = new Mock<IHubContext<ParentHub, IParentClientCallback>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var snapshot = new UserAccountListDto(2, null, []);

        await new UserAccountsBroadcaster(hub.Object).BroadcastAsync(snapshot);

        parents.Verify(p => p.OnUserAccountsChanged(snapshot), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_Null_ThrowsArgumentNullException()
    {
        var broadcaster = new UserAccountsBroadcaster(Mock.Of<IHubContext<ParentHub, IParentClientCallback>>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.BroadcastAsync(null!));
    }
}
