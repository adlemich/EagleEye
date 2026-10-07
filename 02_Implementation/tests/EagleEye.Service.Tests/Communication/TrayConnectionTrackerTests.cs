using EagleEye.Service.Communication;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class TrayConnectionTrackerTests
{
    [Fact]
    public void Count_Initially_IsZero()
    {
        Assert.Equal(0, new TrayConnectionTracker().Count);
    }

    [Fact]
    public void Increment_TwiceThenDecrement_CountsOne()
    {
        var tracker = new TrayConnectionTracker();

        tracker.Increment();
        tracker.Increment();
        tracker.Decrement();

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public async Task Increment_Concurrently_CountsEveryCall()
    {
        var tracker = new TrayConnectionTracker();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(tracker.Increment)));

        Assert.Equal(100, tracker.Count);
    }
}
