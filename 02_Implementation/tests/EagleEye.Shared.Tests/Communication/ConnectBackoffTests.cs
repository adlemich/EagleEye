using EagleEye.Shared.Communication;
using Xunit;

namespace EagleEye.Shared.Tests.Communication;

public sealed class ConnectBackoffTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void GetDelay_EarlyAttempts_DoublesDelay(int attempt, int expectedSeconds)
    {
        var delay = ConnectBackoff.GetDelay(attempt);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(31)]
    [InlineData(int.MaxValue)]
    public void GetDelay_LaterAttempts_CapsAt30Seconds(int attempt)
    {
        var delay = ConnectBackoff.GetDelay(attempt);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void GetDelay_NegativeAttempt_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectBackoff.GetDelay(-1));
    }
}
