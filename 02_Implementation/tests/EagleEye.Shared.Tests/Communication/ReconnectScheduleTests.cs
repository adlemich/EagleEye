using EagleEye.Shared.Communication;
using Xunit;

namespace EagleEye.Shared.Tests.Communication;

public sealed class ReconnectScheduleTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 10)]
    public void GetDelay_FirstRetries_UsesInitialDelays(long previousRetryCount, int expectedSeconds)
    {
        var delay = ReconnectSchedule.GetDelay(previousRetryCount);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(10_000)]
    [InlineData(long.MaxValue)]
    public void GetDelay_LaterRetries_KeepsRetryingEvery30Seconds(long previousRetryCount)
    {
        var delay = ReconnectSchedule.GetDelay(previousRetryCount);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void GetDelay_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReconnectSchedule.GetDelay(-1));
    }
}
