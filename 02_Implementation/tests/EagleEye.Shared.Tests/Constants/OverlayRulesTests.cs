using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class OverlayRulesTests
{
    [Fact]
    public void MaxThresholdMinutes_IsSixty()
    {
        Assert.Equal(60, OverlayRules.MaxThresholdMinutes);
    }

    [Theory]
    [InlineData(-5, OverlayLevel.Expired)]
    [InlineData(0, OverlayLevel.Expired)]
    [InlineData(1, OverlayLevel.Final)]
    [InlineData(120, OverlayLevel.Final)]
    [InlineData(121, OverlayLevel.Early)]
    [InlineData(300, OverlayLevel.Early)]
    [InlineData(301, OverlayLevel.Normal)]
    [InlineData(3600, OverlayLevel.Normal)]
    public void LevelFor_EarlyFiveFinalTwo_Boundaries(int seconds, OverlayLevel expected)
    {
        Assert.Equal(expected, OverlayRules.LevelFor(seconds, 5, 2));
    }
}
