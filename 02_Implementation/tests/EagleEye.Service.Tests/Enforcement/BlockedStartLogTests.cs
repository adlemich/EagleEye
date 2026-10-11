using EagleEye.Service.Enforcement;
using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static EagleEye.Service.Tests.Enforcement.EnforcementTestData;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class BlockedStartLogTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly TestLogger<BlockedStartLog> _logger = new();
    private readonly BlockedStartLog _log;

    public BlockedStartLogTests()
    {
        _log = new BlockedStartLog(_logger, new InstanceLogLimiter(_time));
    }

    [Fact]
    public void Detected_LogsAllAc28Fields()
    {
        Assert.True(_log.Detected(Start()));

        Assert.Equal(
            @"Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 5120, break time 20:00–23:59 (Mo Tu We Th Fr Sa Su); closing the app.",
            _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public void Ended_LogsOutcomeAndMessageState()
    {
        _log.Ended(Start(), "closed gracefully after 0.5 s", "shown", detectionLogged: true);

        Assert.Equal(
            @"Blocked start ended: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), break time 20:00–23:59 (Mo Tu We Th Fr Sa Su): closed gracefully after 0.5 s; message shown.",
            _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public void Ended_DetectionSuppressed_NothingLogged()
    {
        _log.Ended(Start(), "x", "shown", detectionLogged: false);

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Limiter_After30PerHour_SuppressedThenOneSummary()
    {
        var results = Enumerable.Range(0, 33).Select(_ => _log.Detected(Start())).ToList();
        _log.Flush();
        var beforeHour = _logger.Messages(LogLevel.Information).Count;
        _time.Advance(InstanceLogLimiter.Window);
        _log.Flush();

        Assert.Equal((30, 3, 30), (results.Count(r => r), results.Count(r => !r), beforeHour));
        Assert.Equal(
            @"3 further blocked starts of account kid1, Editor (C:\Windows\System32\notepad.exe) were not logged in the last hour.",
            _logger.Messages(LogLevel.Information).Last());
    }

    [Fact]
    public void ErrorsAndWarnings()
    {
        var failure = new InvalidOperationException("db");

        _log.NotEnded(Start(), 2);
        _log.TerminateFailed(Start(), 5120, 5);
        _log.HistoryFailed(Start(), failure);

        Assert.Equal(
            @"The blocked app Editor (C:\Windows\System32\notepad.exe) of account kid1 could not be ended: 2 process(es) still running after 3 rounds.",
            _logger.Messages(LogLevel.Error).Single());
        Assert.Contains(@"Process 5120 of the blocked app Editor (C:\Windows\System32\notepad.exe) could not be terminated (Win32 error 5).", _logger.Messages(LogLevel.Warning));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _log.Detected(null!));
        Assert.Throws<ArgumentNullException>(() => _log.Ended(null!, "o", "s", true));
        Assert.Throws<ArgumentNullException>(() => _log.NotEnded(null!, 1));
        Assert.Throws<ArgumentNullException>(() => _log.TerminateFailed(null!, 1, 1));
        Assert.Throws<ArgumentNullException>(() => _log.HistoryFailed(null!, new InvalidOperationException()));
    }
}
