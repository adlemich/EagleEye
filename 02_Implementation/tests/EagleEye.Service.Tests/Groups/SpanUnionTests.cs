using EagleEye.Service.Groups;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class SpanUnionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Seconds_Empty_Zero()
    {
        Assert.Equal(0, SpanUnion.Seconds([]));
    }

    [Fact]
    public void Seconds_TogetherFiveMinutes_Five()
    {
        // AC-19 example 1: Notepad and Paint run together for 5 minutes.
        Assert.Equal(300, SpanUnion.Seconds([M(0, 5), M(0, 5)]));
    }

    [Fact]
    public void Seconds_TwoThenBothTen_Twelve()
    {
        // AC-19 example 2: Notepad alone 2 minutes, then Paint starts and both run 10 minutes.
        Assert.Equal(720, SpanUnion.Seconds([M(0, 12), M(2, 12)]));
    }

    [Fact]
    public void Seconds_ThreeThenFour_Seven()
    {
        // AC-19 example 3: Notepad 3 minutes, closed, then Paint 4 minutes.
        Assert.Equal(420, SpanUnion.Seconds([M(3, 7), M(0, 3)]));
    }

    [Fact]
    public void Seconds_Containment_CountsTheOuterSpan()
    {
        Assert.Equal(600, SpanUnion.Seconds([M(0, 10), M(2, 4)]));
    }

    [Fact]
    public void Seconds_Gap_CountsBoth()
    {
        Assert.Equal(240, SpanUnion.Seconds([M(0, 2), M(5, 7)]));
    }

    [Fact]
    public void Seconds_PartialSecond_RoundedDown()
    {
        Assert.Equal(1, SpanUnion.Seconds([(T0, T0.AddMilliseconds(1999))]));
    }

    [Fact]
    public void Seconds_Guard()
    {
        Assert.Throws<ArgumentNullException>(() => SpanUnion.Seconds(null!));
    }

    [Fact]
    public void Seconds_TwentyGroupsOfHundredSpans_FastEnough()
    {
        // AC-41 guard: the union of 20 × 100 spans takes well under a millisecond each.
        var spans = Enumerable.Range(0, 100).Select(i => (T0.AddSeconds(i * 10), T0.AddSeconds((i * 10) + 7))).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var group = 0; group < 20; group++)
        {
            Assert.Equal(700, SpanUnion.Seconds(spans));
        }

        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(200), $"took {watch.Elapsed.TotalMilliseconds} ms");
    }

    private static (DateTimeOffset, DateTimeOffset) M(int fromMinute, int toMinute) => (T0.AddMinutes(fromMinute), T0.AddMinutes(toMinute));
}
