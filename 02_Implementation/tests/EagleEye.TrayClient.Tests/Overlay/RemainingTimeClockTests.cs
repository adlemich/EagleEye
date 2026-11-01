using EagleEye.Shared.Models;
using EagleEye.TrayClient.Overlay;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.TrayClient.Tests.Overlay;

public sealed class RemainingTimeClockTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly RemainingTimeClock _clock;

    public RemainingTimeClockTests()
    {
        _clock = new RemainingTimeClock(_time);
    }

    [Fact]
    public void NoModel_NullAndDefaultMinutes()
    {
        Assert.Null(_clock.Current());
        Assert.Equal((false, 5, 1), (_clock.HasModel, _clock.EarlyMinutes, _clock.FinalMinutes));
    }

    [Fact]
    public void Countdown_CountingLinesPerWholeSecond_OthersStay_FloorZero()
    {
        _clock.Update(Model(new OverlayLineDto(OverlayLineKind.App, "Editor", 180, OverlayLevel.Early, true), new OverlayLineDto(OverlayLineKind.App, "Paint", 90, OverlayLevel.Normal, false),
            new OverlayLineDto(OverlayLineKind.Break, null, 2, OverlayLevel.Final, true)));

        _time.Advance(TimeSpan.FromMilliseconds(3900));

        Assert.Equal(
            [new DisplayedLine(OverlayLineKind.App, "Editor", 177), new DisplayedLine(OverlayLineKind.App, "Paint", 90), new DisplayedLine(OverlayLineKind.Break, null, 0)],
            _clock.Current());
        Assert.Equal((7, 2, true), (_clock.EarlyMinutes, _clock.FinalMinutes, _clock.HasModel));
    }

    [Fact]
    public void Stale_After15Seconds_Dropped()
    {
        _clock.Update(Model(new OverlayLineDto(OverlayLineKind.App, "Editor", 180, OverlayLevel.Early, true)));

        _time.Advance(TimeSpan.FromMilliseconds(14_999));
        Assert.NotNull(_clock.Current());

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(_clock.Current());
        Assert.False(_clock.HasModel);
    }

    [Fact]
    public void Update_NewModel_RestartsTheCountdown()
    {
        _clock.Update(Model(new OverlayLineDto(OverlayLineKind.App, "Editor", 180, OverlayLevel.Early, true)));
        _time.Advance(TimeSpan.FromSeconds(10));

        _clock.Update(Model(new OverlayLineDto(OverlayLineKind.App, "Editor", 171, OverlayLevel.Early, true)));

        Assert.Equal(171, _clock.Current()!.Single().Seconds);
    }

    [Fact]
    public void Clear_Dropped()
    {
        _clock.Update(Model());

        _clock.Clear();

        Assert.Null(_clock.Current());
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(300, 720)]
    [InlineData(990, 30)]
    [InlineData(999, 21)]
    [InlineData(1500, 520)]
    public void UntilNextSecond_NextBoundaryPlus20ms_Clamped(int elapsedMs, int expectedMs)
    {
        _clock.Update(Model());
        _time.Advance(TimeSpan.FromMilliseconds(elapsedMs));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), _clock.UntilNextSecond());
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RemainingTimeClock(null!));
        Assert.Throws<ArgumentNullException>(() => _clock.Update(null!));
    }

    private static RemainingTimeDto Model(params OverlayLineDto[] lines) => new(lines, 7, 2, OverlaySignal.None);
}
