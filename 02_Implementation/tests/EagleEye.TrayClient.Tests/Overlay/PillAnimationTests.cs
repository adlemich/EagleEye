using EagleEye.Shared.Models;
using EagleEye.TrayClient.Overlay;
using Xunit;

namespace EagleEye.TrayClient.Tests.Overlay;

public sealed class PillAnimationTests
{
    [Theory]
    [InlineData(25, 64)]
    [InlineData(50, 128)]
    [InlineData(75, 191)]
    [InlineData(100, 255)]
    public void NoPulse_BaseOpacity(int opacity, byte expected)
    {
        Assert.Equal(new PillPulse(expected, false), PillAnimation.Compute(opacity, null, false, TimeSpan.FromSeconds(1.3)));
    }

    [Fact]
    public void QuickPulse_ThreeSecondsAt4Hz_ThenOver()
    {
        // 4 Hz: phase 1 at t = 1/16 s (sin = 1), phase 0 at t = 3/16 s (sin = -1).
        Assert.Equal(new PillPulse(230, true), PillAnimation.Compute(75, TimeSpan.FromSeconds(1), false, TimeSpan.FromSeconds(1.0 / 16)));
        Assert.Equal(new PillPulse(48, true), PillAnimation.Compute(75, TimeSpan.FromSeconds(2.9), false, TimeSpan.FromSeconds(3.0 / 16)));
        Assert.Equal(new PillPulse(191, false), PillAnimation.Compute(75, TimeSpan.FromSeconds(3), false, TimeSpan.FromSeconds(1.0 / 16)));
    }

    [Fact]
    public void SlowPulse_WhileFinalAboveZero_1Hz()
    {
        Assert.Equal(new PillPulse(255, true), PillAnimation.Compute(100, null, true, TimeSpan.FromSeconds(0.25)));
        Assert.Equal(new PillPulse(64, true), PillAnimation.Compute(100, TimeSpan.FromSeconds(10), true, TimeSpan.FromSeconds(0.75)));
    }

    [Fact]
    public void QuickPulseOverridesTheSlowPulse()
    {
        // At t = 1/16 s the 1-Hz phase is not 1, the 4-Hz phase is.
        Assert.Equal(230, PillAnimation.Compute(75, TimeSpan.Zero, true, TimeSpan.FromSeconds(1.0 / 16)).BackgroundAlpha);
    }

    [Theory]
    [InlineData(OverlaySignal.None, true, false)]
    [InlineData(OverlaySignal.Early, true, true)]
    [InlineData(OverlaySignal.Final, true, true)]
    [InlineData(OverlaySignal.Expired, true, true)]
    [InlineData(OverlaySignal.Final, false, false)]
    public void PlaysSound_OnlyWithASignalAndTheSoundOn(OverlaySignal signal, bool soundOn, bool expected)
    {
        Assert.Equal(expected, PillAnimation.PlaysSound(signal, soundOn));
    }

    [Theory]
    [InlineData(OverlaySignal.None, false)]
    [InlineData(OverlaySignal.Early, true)]
    [InlineData(OverlaySignal.Final, false)]
    [InlineData(OverlaySignal.Expired, false)]
    public void StartsQuickPulse_EarlyOnly(OverlaySignal signal, bool expected)
    {
        Assert.Equal(expected, PillAnimation.StartsQuickPulse(signal));
    }
}
