using EagleEye.TrayClient.Communication;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace EagleEye.TrayClient.Tests.Communication;

public sealed class ServiceReconnectPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 10)]
    public void NextRetryDelay_FirstRetries_UsesInitialDelays(long previousRetryCount, int expectedSeconds)
    {
        var policy = new ServiceReconnectPolicy();

        var delay = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = previousRetryCount });

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(10_000)]
    public void NextRetryDelay_LaterRetries_KeepsRetryingEvery30Seconds(long previousRetryCount)
    {
        var policy = new ServiceReconnectPolicy();

        var delay = policy.NextRetryDelay(new RetryContext { PreviousRetryCount = previousRetryCount });

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void NextRetryDelay_NullContext_ThrowsArgumentNullException()
    {
        var policy = new ServiceReconnectPolicy();

        Assert.Throws<ArgumentNullException>(() => policy.NextRetryDelay(null!));
    }
}
