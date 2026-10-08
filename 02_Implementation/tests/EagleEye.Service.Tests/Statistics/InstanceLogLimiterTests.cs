using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class InstanceLogLimiterTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Notepad = @"C:\Windows\notepad.exe";

    private readonly FakeTimeProvider _time = new();
    private readonly InstanceLogLimiter _limiter;

    public InstanceLogLimiterTests()
    {
        _limiter = new InstanceLogLimiter(_time);
    }

    [Fact]
    public void TryAcquire_30PerHour_ThenSuppressed()
    {
        var allowed = Enumerable.Range(0, 40).Count(_ => Acquire());

        Assert.Equal(30, allowed);
    }

    [Fact]
    public void TryAcquire_OtherAppOrAccount_OwnLimit()
    {
        for (var i = 0; i < 30; i++)
        {
            Acquire();
        }

        Assert.Equal(
            (false, true, true),
            (Acquire(), _limiter.TryAcquire(Kid, @"C:\x\game.exe", "kid1", "Game"), _limiter.TryAcquire("S-1-5-21-9", Notepad, "kid2", "Editor")));
    }

    [Fact]
    public void TryAcquire_PathCaseIgnored()
    {
        for (var i = 0; i < 30; i++)
        {
            Acquire();
        }

        Assert.False(_limiter.TryAcquire(Kid.ToLowerInvariant(), Notepad.ToUpperInvariant(), "kid1", "Editor"));
    }

    [Fact]
    public void TakeSummaries_AfterHour_OneSummaryWithCount()
    {
        for (var i = 0; i < 35; i++)
        {
            Acquire();
        }

        var during = _limiter.TakeSummaries();
        _time.Advance(InstanceLogLimiter.Window);

        Assert.Empty(during);
        Assert.Equal([new SuppressedEntries("kid1", "Editor", Notepad, 5)], _limiter.TakeSummaries());
        Assert.Empty(_limiter.TakeSummaries());
    }

    [Fact]
    public void TakeSummaries_HourOverWithoutSuppression_Nothing()
    {
        Acquire();
        _time.Advance(InstanceLogLimiter.Window);

        Assert.Empty(_limiter.TakeSummaries());
    }

    [Fact]
    public void TryAcquire_NewHour_AllowsAgainAndKeepsSummaryOfOldHour()
    {
        for (var i = 0; i < 31; i++)
        {
            Acquire();
        }

        _time.Advance(InstanceLogLimiter.Window);
        var allowed = Acquire();

        Assert.Equal((true, 1), (allowed, _limiter.TakeSummaries().Single().Count));
    }

    [Fact]
    public void TryAcquire_NewHourWithoutSuppression_NoSummary()
    {
        Acquire();
        _time.Advance(InstanceLogLimiter.Window);
        Acquire();

        Assert.Empty(_limiter.TakeSummaries());
    }

    private bool Acquire() => _limiter.TryAcquire(Kid, Notepad, "kid1", "Editor");
}
