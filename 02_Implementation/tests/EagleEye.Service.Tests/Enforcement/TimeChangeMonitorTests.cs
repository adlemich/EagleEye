using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Tests.Statistics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class TimeChangeMonitorTests
{
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.CreateCustomTimeZone("Test Pacific", TimeSpan.FromHours(-8), "Test Pacific", "Test Pacific");
    private static readonly SessionInUse Kid = new(2, "S-1-5-21-1-2-3-1003", "kid1");
    private static readonly SessionInUse Kid2 = new(3, "S-1-5-21-1-2-3-1004", "kid2");

    private readonly ClockAndZone _time = new(new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero), TestZones.Berlin);
    private readonly List<TimeChangeFinding> _findings = [];
    private readonly Mock<ITimeChangeFindingRepository> _repository = new();
    private readonly TestLogger<TimeChangeMonitor> _logger = new();
    private readonly TimeChangeMonitor _monitor;

    public TimeChangeMonitorTests()
    {
        _repository.Setup(r => r.InsertAsync(It.IsAny<TimeChangeFinding>(), It.IsAny<CancellationToken>()))
            .Callback<TimeChangeFinding, CancellationToken>((f, _) => _findings.Add(f)).Returns(Task.CompletedTask);
        _monitor = new TimeChangeMonitor(_repository.Object, _time, _logger);
    }

    [Fact]
    public async Task FirstTick_BaselineOnly()
    {
        await _monitor.OnTickAsync([Kid]);

        Assert.Empty(_findings);
    }

    [Fact]
    public async Task ZoneChanged_WarningAndFindingWithTheSessionInUse()
    {
        await _monitor.OnTickAsync([Kid]);
        _time.Zone = Pacific;
        _time.Advance(TimeSpan.FromSeconds(5));

        await _monitor.OnTickAsync([Kid]);

        var finding = _findings.Single();
        Assert.Equal(
            (TimeChangeMonitor.TimeZoneKind, "Test Berlin (UTC+01:00)", "Test Pacific (UTC-08:00, no DST)", (int?)2, (string?)Kid.AccountSid, (string?)"kid1", new DateTime(2026, 10, 12, 10, 30, 5)),
            (finding.Kind, finding.OldValue, finding.NewValue, finding.SessionId, finding.AccountSid, finding.UserName, finding.DetectedLocal));
        Assert.Equal(
            "The time zone of the PC changed from Test Berlin (UTC+01:00) to Test Pacific (UTC-08:00, no DST); break times now use the new local time. Session in use: kid1.",
            _logger.Messages(LogLevel.Warning).Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ZoneChanged_NoneOrSeveralSessionsInUse_NoAccount(int sessions)
    {
        SessionInUse[] inUse = sessions == 0 ? [] : [Kid, Kid2];
        await _monitor.OnTickAsync(inUse);
        _time.Zone = Pacific;

        await _monitor.OnTickAsync(inUse);

        Assert.Equal(((int?)null, (string?)null), (_findings.Single().SessionId, _findings.Single().AccountSid));
        Assert.EndsWith("break times now use the new local time.", _logger.Messages(LogLevel.Warning).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DstSwitchedOff_IsAFinding()
    {
        await _monitor.OnTickAsync([]);
        _time.Zone = TimeZoneInfo.CreateCustomTimeZone("Test Berlin", TimeSpan.FromHours(1), "Test Berlin", "Test Berlin");

        await _monitor.OnTickAsync([]);

        Assert.Equal("Test Berlin (UTC+01:00, no DST)", _findings.Single().NewValue);
    }

    [Fact]
    public async Task RegularDstTransition_NoFinding()
    {
        _time.WallNow = new DateTimeOffset(2026, 10, 25, 0, 59, 58, TimeSpan.Zero);
        await _monitor.OnTickAsync([]);
        _time.Advance(TimeSpan.FromSeconds(5));

        await _monitor.OnTickAsync([]);

        Assert.Empty(_findings);
    }

    [Fact]
    public async Task ClockJumpOver30s_Finding()
    {
        await _monitor.OnTickAsync([Kid]);
        _time.Advance(TimeSpan.FromSeconds(5));
        _time.WallNow += TimeSpan.FromHours(-2);

        await _monitor.OnTickAsync([Kid]);

        var finding = _findings.Single();
        Assert.Equal((TimeChangeMonitor.ClockKind, "2026-10-12 20:30:05", "2026-10-12 18:30:05"), (finding.Kind, finding.OldValue, finding.NewValue));
        Assert.StartsWith("The clock of the PC changed from 2026-10-12 20:30:05 to 2026-10-12 18:30:05", _logger.Messages(LogLevel.Warning).Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(-30)]
    public async Task ClockCorrectionUpTo30s_Ignored(int seconds)
    {
        await _monitor.OnTickAsync([]);
        _time.Advance(TimeSpan.FromSeconds(5));
        _time.WallNow += TimeSpan.FromSeconds(seconds);

        await _monitor.OnTickAsync([]);

        Assert.Empty(_findings);
    }

    [Fact]
    public async Task LongGap_NotJudged()
    {
        await _monitor.OnTickAsync([]);
        _time.AdvanceMonotonic(TimeSpan.FromSeconds(16));
        _time.WallNow += TimeSpan.FromHours(1);

        await _monitor.OnTickAsync([]);

        Assert.Empty(_findings);
    }

    [Fact]
    public async Task Rebaseline_SuspendResume_NotJudged()
    {
        await _monitor.OnTickAsync([]);
        _monitor.Rebaseline();
        _time.Advance(TimeSpan.FromSeconds(5));
        _time.WallNow += TimeSpan.FromHours(1);

        await _monitor.OnTickAsync([]);
        _time.Advance(TimeSpan.FromSeconds(5));
        await _monitor.OnTickAsync([]);

        Assert.Empty(_findings);
    }

    [Fact]
    public async Task StorageFails_WarningAndGoesOn()
    {
        var failure = new InvalidOperationException("db");
        _repository.Setup(r => r.InsertAsync(It.IsAny<TimeChangeFinding>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        await _monitor.OnTickAsync([]);
        _time.Zone = Pacific;

        await _monitor.OnTickAsync([]);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public async Task Cancelled_Propagates()
    {
        _repository.Setup(r => r.InsertAsync(It.IsAny<TimeChangeFinding>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        await _monitor.OnTickAsync([]);
        _time.Zone = Pacific;

        await Assert.ThrowsAsync<OperationCanceledException>(() => _monitor.OnTickAsync([]));
    }

    [Fact]
    public async Task Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _monitor.OnTickAsync(null!));
    }

    /// <summary>A clock whose zone, wall time and monotonic time can be changed independently.</summary>
    private sealed class ClockAndZone(DateTimeOffset wallNow, TimeZoneInfo zone) : TimeProvider
    {
        private long _timestamp = 1_000_000;

        public DateTimeOffset WallNow { get; set; } = wallNow;

        public TimeZoneInfo Zone { get; set; } = zone;

        public override TimeZoneInfo LocalTimeZone => Zone;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => WallNow;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan time)
        {
            _timestamp += time.Ticks;
            WallNow += time;
        }

        public void AdvanceMonotonic(TimeSpan time) => _timestamp += time.Ticks;
    }
}
