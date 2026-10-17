using EagleEye.Shared.Constants;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class WarningRulesTests
{
    [Fact]
    public void Defaults_FiveAndOne()
    {
        Assert.Equal((5, 1, 1, 59), (WarningRules.DefaultEarlyMinutes, WarningRules.DefaultFinalMinutes, WarningRules.MinMinutes, WarningRules.MaxMinutes));
        Assert.True(WarningRules.IsValidPair(WarningRules.DefaultEarlyMinutes, WarningRules.DefaultFinalMinutes));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    public void IsValidMinutes_Range(int minutes, bool expected)
    {
        Assert.Equal(expected, WarningRules.IsValidMinutes(minutes));
    }

    [Theory]
    [InlineData(3, 3, false)]
    [InlineData(3, 4, false)]
    [InlineData(2, 1, true)]
    [InlineData(59, 58, true)]
    [InlineData(60, 1, false)]
    [InlineData(5, 0, false)]
    public void IsValidPair_EarlyGreaterThanFinal(int early, int final, bool expected)
    {
        Assert.Equal(expected, WarningRules.IsValidPair(early, final));
    }
}
