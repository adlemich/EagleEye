using EagleEye.ParentApp.Core.Communication;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentReconnectPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 10)]
    [InlineData(3, 30)]
    [InlineData(1000, 30)]
    public void NextRetryDelay_FollowsReconnectSchedule(long previousRetryCount, int expectedSeconds)
    {
        var delay = new ParentReconnectPolicy().NextRetryDelay(new RetryContext { PreviousRetryCount = previousRetryCount });

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void NextRetryDelay_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ParentReconnectPolicy().NextRetryDelay(null!));
    }
}
