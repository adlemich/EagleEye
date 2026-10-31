using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentHubGatewayAppGroupsTests
{
    private static readonly AppGroupsDto Groups = new(2, null, [], []);
    private static readonly AppGroupRulesDto Rules = new(3, null, "S-1-5-21-1", []);

    [Fact]
    public void AppGroupsAndRules_ForwardedOnlyFromCurrentClientWhileConnected()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var first = new Mock<IParentHubClient>();
        var second = new Mock<IParentHubClient>();
        var groups = new List<AppGroupsDto>();
        var rules = new List<AppGroupRulesDto>();
        gateway.AppGroupsChanged += groups.Add;
        gateway.AppGroupRulesChanged += rules.Add;

        gateway.SetConnected(first.Object);
        first.Raise(c => c.AppGroupsChanged += null, Groups);
        first.Raise(c => c.AppGroupRulesChanged += null, Rules);
        gateway.SetDisconnected();
        first.Raise(c => c.AppGroupsChanged += null, Groups);
        gateway.SetConnected(second.Object);
        first.Raise(c => c.AppGroupRulesChanged += null, Rules);
        second.Raise(c => c.AppGroupsChanged += null, Groups);
        second.Raise(c => c.AppGroupRulesChanged += null, Rules);

        Assert.Equal((2, 2), (groups.Count, rules.Count));
        first.VerifyRemove(c => c.AppGroupsChanged -= It.IsAny<Action<AppGroupsDto>>(), Times.Once);
        first.VerifyRemove(c => c.AppGroupRulesChanged -= It.IsAny<Action<AppGroupRulesDto>>(), Times.Once);
    }

    [Fact]
    public void Broadcasts_WithoutHandlers_DoNotThrow()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var client = new Mock<IParentHubClient>();
        gateway.SetConnected(client.Object);

        client.Raise(c => c.AppGroupsChanged += null, Groups);
        client.Raise(c => c.AppGroupRulesChanged += null, Rules);

        Assert.True(gateway.IsConnected);
    }
}
