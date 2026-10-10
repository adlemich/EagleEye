using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentHubGatewayRulesTests
{
    private static readonly AccountRulesDto Rules = new(2, null, "S-1-5-21-1", [], "x", false);

    [Fact]
    public void AccountRulesChanged_ForwardedOnlyFromCurrentClientWhileConnected()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var first = new Mock<IParentHubClient>();
        var second = new Mock<IParentHubClient>();
        var received = new List<AccountRulesDto>();
        gateway.AccountRulesChanged += received.Add;

        gateway.SetConnected(first.Object);
        first.Raise(c => c.AccountRulesChanged += null, Rules);
        gateway.SetDisconnected();
        first.Raise(c => c.AccountRulesChanged += null, Rules);
        gateway.SetConnected(second.Object);
        first.Raise(c => c.AccountRulesChanged += null, Rules);
        second.Raise(c => c.AccountRulesChanged += null, Rules);

        Assert.Equal(2, received.Count);
        first.VerifyRemove(c => c.AccountRulesChanged -= It.IsAny<Action<AccountRulesDto>>(), Times.Once);
    }

    [Fact]
    public void AccountRulesChanged_WithoutHandler_DoesNotThrow()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var client = new Mock<IParentHubClient>();
        gateway.SetConnected(client.Object);

        client.Raise(c => c.AccountRulesChanged += null, Rules);

        Assert.True(gateway.IsConnected);
    }
}
