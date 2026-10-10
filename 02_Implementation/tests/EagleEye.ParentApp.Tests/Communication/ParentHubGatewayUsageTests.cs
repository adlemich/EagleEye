using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentHubGatewayUsageTests
{
    private static readonly DayUsageDto Day = new(2, null, "S-1-5-21-1", new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7), []);

    [Fact]
    public void DayUsageChanged_ForwardedOnlyFromCurrentClientWhileConnected()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var first = new Mock<IParentHubClient>();
        var second = new Mock<IParentHubClient>();
        var received = new List<DayUsageDto>();
        gateway.DayUsageChanged += received.Add;

        gateway.SetConnected(first.Object);
        first.Raise(c => c.DayUsageChanged += null, Day);
        gateway.SetDisconnected();
        first.Raise(c => c.DayUsageChanged += null, Day);
        gateway.SetConnected(second.Object);
        first.Raise(c => c.DayUsageChanged += null, Day);
        second.Raise(c => c.DayUsageChanged += null, Day);

        Assert.Equal(2, received.Count);
        first.VerifyRemove(c => c.DayUsageChanged -= It.IsAny<Action<DayUsageDto>>(), Times.Once);
    }

    [Fact]
    public void DayUsageChanged_WithoutHandler_DoesNotThrow()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var client = new Mock<IParentHubClient>();
        gateway.SetConnected(client.Object);

        client.Raise(c => c.DayUsageChanged += null, Day);

        Assert.True(gateway.IsConnected);
    }
}
