using System.Threading.Channels;
using EagleEye.Service.Monitoring;
using EagleEye.Service.Statistics;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageEventQueueTests
{
    private readonly UsageEventQueue _queue = new();

    [Fact]
    public async Task NonReportEvents_InOrder()
    {
        UsageEvent[] events = [new UsageTick(), new SessionChanged(2, SessionChangeKind.Lock), new PowerSuspended(), new PowerResumed()];
        foreach (var usageEvent in events)
        {
            _queue.Enqueue(usageEvent);
        }

        UsageEvent[] read = [await Next(), await Next(), await Next(), await Next()];

        Assert.Equal<IEnumerable<UsageEvent>>(events, read);
    }

    [Fact]
    public async Task Reports_LatestPerSessionOnly()
    {
        _queue.Enqueue(Report(2, 1));
        _queue.Enqueue(Report(3, 1));
        _queue.Enqueue(Report(2, 2));

        var first = (AppsObserved)await Next();
        var second = (AppsObserved)await Next();

        Assert.Equal((2, 2, 3), (first.SessionId, first.Apps.Count, second.SessionId));
        Assert.Equal(0, _queue.PendingReports);
    }

    [Fact]
    public void Reports_BurstStaysBounded()
    {
        for (var i = 0; i < 10_000; i++)
        {
            _queue.Enqueue(Report(2, i % 5));
        }

        Assert.Equal(1, _queue.PendingReports);
    }

    [Fact]
    public async Task Reports_AfterProcessing_NewMarker()
    {
        _queue.Enqueue(Report(2, 1));
        await Next();

        Assert.True(_queue.Enqueue(Report(2, 3)));
        Assert.Equal(3, ((AppsObserved)await Next()).Apps.Count);
    }

    [Fact]
    public async Task Complete_RejectsNewEventsAndEndsReading()
    {
        _queue.Complete();

        Assert.False(_queue.Enqueue(new UsageTick()));
        Assert.False(_queue.Enqueue(Report(2, 1)));
        await Assert.ThrowsAsync<ChannelClosedException>(() => _queue.DequeueAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public void Enqueue_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _queue.Enqueue(null!));
    }

    private async Task<UsageEvent> Next() => await _queue.DequeueAsync(CancellationToken.None);

    private static AppsObserved Report(int session, int apps) =>
        new(session, "S-1-5-21-1-2-3-1003", [.. Enumerable.Range(0, apps).Select(i => new ObservedApp($@"C:\a\{i}.exe", $"{i}.exe", $"{i}", [i + 1]))]);
}
