using EagleEye.Service.Communication;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageBroadcasterTests
{
    [Fact]
    public async Task BroadcastAsync_SendsDaySnapshotToParentsGroup()
    {
        var parents = new Mock<IParentClientCallback>();
        var clients = new Mock<IHubClients<IParentClientCallback>>();
        clients.Setup(c => c.Group(ParentHub.ParentsGroup)).Returns(parents.Object);
        var hub = new Mock<IHubContext<ParentHub, IParentClientCallback>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        var snapshot = new DayUsageDto(2, null, "S-1-5-21-1", new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7), []);

        await new UsageBroadcaster(hub.Object).BroadcastAsync(snapshot);

        parents.Verify(p => p.OnDayUsageChanged(snapshot), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_Null_ThrowsArgumentNullException()
    {
        var broadcaster = new UsageBroadcaster(Mock.Of<IHubContext<ParentHub, IParentClientCallback>>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.BroadcastAsync(null!));
    }
}
