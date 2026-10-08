using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageTrackerTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\notepad.exe";
    private const string Paint = @"C:\Windows\System32\mspaint.exe";

    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero); // 12:00 local
    private static readonly DateOnly Oct7 = new(2026, 10, 7);
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private readonly ManualTimeProvider _time = new(Start, TestZones.Berlin);
    private readonly TestLogger<UsageTracker> _logger = new();
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly UsageTracker _tracker;

    public UsageTrackerTests()
    {
        _tracker = new UsageTracker(_time, _logger);
    }

    [Fact]
    public void ObserveApps_NewApp_StartsInstance()
    {
        Observe(2, Kid, Notepad);

        var started = Assert.IsType<InstanceStarted>(Assert.Single(_tracker.TakeOutput(false).Instances));
        Assert.Equal((Kid, Notepad, "notepad.exe", "Editor", Start), (started.AccountSid, started.ProgramPath, started.ProcessName, started.DisplayName, started.StartedUtc));
    }

    [Fact]
    public void Tick_Active_CreditsSeconds()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);

        Assert.Equal([new UsageCredit(Kid, Notepad, Oct7, 5)], Tick().Credits);
    }

    [Fact]
    public void Tick_Inactive_NoCredit()
    {
        Observe(2, Kid, Notepad);
        _tracker.SetActivity(_ => false);

        Assert.Empty(Tick().Credits);
    }

    [Fact]
    public void AccuracyScenario_13MinutesOpen3Locked_600Seconds()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        var total = 0L;
        for (var tick = 1; tick <= 13 * 12; tick++)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            _tracker.Advance();
            var minute = tick / 12;
            _tracker.SetActivity(_ => minute is < 5 or >= 8); // locked from minute 5 to 8
            total += _tracker.TakeOutput(true).Credits.Sum(c => c.Seconds);
        }

        Assert.InRange(total, 595, 605);
    }

    [Fact]
    public void ParallelApps_BothCredited()
    {
        Observe(2, Kid, Notepad, Paint);
        Activate(Kid);

        Assert.Equal(2, Tick().Credits.Count(c => c.Seconds == 5));
    }

    [Fact]
    public void SameAppInTwoSessions_OneInstanceCreditedOnce()
    {
        Observe(2, Kid, Notepad);
        Observe(4, Kid, Notepad);
        Activate(Kid);

        var output = Tick();

        Assert.Single(output.Instances);
        Assert.Equal(5, output.Credits.Single().Seconds);
    }

    [Fact]
    public void Close_EndsAfterMergeWindowAtTimeOfClosing()
    {
        Observe(2, Kid, Notepad);
        _time.Advance(TimeSpan.FromSeconds(10));
        Observe(2, Kid);
        _tracker.TakeOutput(false);

        _time.Advance(TimeSpan.FromSeconds(4));
        _tracker.Advance();
        var before = _tracker.TakeOutput(false).Instances;
        _time.Advance(TimeSpan.FromSeconds(1));
        _tracker.Advance();

        var ended = Assert.IsType<InstanceEnded>(Assert.Single(_tracker.TakeOutput(false).Instances));
        Assert.Empty(before);
        Assert.Equal((EndReasons.Closed, Start.AddSeconds(10), Start), (ended.Reason, ended.EndedUtc, ended.StartedUtc));
    }

    [Fact]
    public void ClosedApp_NotCreditedWhileGone()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        Observe(2, Kid);

        Assert.Empty(Tick().Credits);
    }

    [Fact]
    public void Reopen_WithinFiveSeconds_SameInstance()
    {
        Observe(2, Kid, Notepad);
        Observe(2, Kid);
        _time.Advance(TimeSpan.FromSeconds(4));
        Observe(2, Kid, Notepad);
        _time.Advance(TimeSpan.FromSeconds(10));
        _tracker.Advance();

        Assert.Single(_tracker.TakeOutput(false).Instances);
    }

    [Fact]
    public void Reopen_AfterSixSeconds_NewInstance()
    {
        Observe(2, Kid, Notepad);
        Observe(2, Kid);
        _time.Advance(TimeSpan.FromSeconds(6));
        Observe(2, Kid, Notepad);

        Assert.Equal(
            [typeof(InstanceStarted), typeof(InstanceEnded), typeof(InstanceStarted)],
            _tracker.TakeOutput(false).Instances.Select(i => i.GetType()));
    }

    [Fact]
    public void AcrossMidnight_SplitIntoTwoDays()
    {
        _time.WallNow = new DateTimeOffset(2026, 10, 7, 21, 59, 57, TimeSpan.Zero); // 23:59:57 local
        Observe(2, Kid, Notepad);
        Activate(Kid);

        Assert.Equal([new UsageCredit(Kid, Notepad, Oct7, 3), new UsageCredit(Kid, Notepad, Oct8, 2)], Tick().Credits);
    }

    [Fact]
    public void Gap_LongerThan15Seconds_NotCreditedAndLoggedOnce()
    {
        Observe(2, Kid, Notepad, Paint);
        Activate(Kid);
        _time.Advance(TimeSpan.FromSeconds(16));
        _tracker.Advance();

        Assert.Empty(_tracker.TakeOutput(true).Credits);
        Assert.Equal("Usage accounting paused for 16 s (sleep, suspension or delay); this time is not counted.", Assert.Single(_logger.Messages(LogLevel.Information)));
    }

    [Fact]
    public void Gap_Exactly15Seconds_Credited()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        _time.Advance(UsageTracker.GapLimit);
        _tracker.Advance();

        Assert.Equal(15, _tracker.TakeOutput(true).Credits.Single().Seconds);
    }

    [Fact]
    public void Gap_WithoutActiveApps_NotLogged()
    {
        Observe(2, Kid, Notepad);
        _time.Advance(TimeSpan.FromMinutes(5));
        _tracker.Advance();

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void SuspendAndResume_SleepNotCounted()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        _time.Advance(TimeSpan.FromSeconds(3));
        _tracker.Suspend();
        _time.Advance(TimeSpan.FromSeconds(10));
        _tracker.Advance();
        _tracker.Resume();
        _time.Advance(TimeSpan.FromSeconds(2));
        _tracker.Advance();

        Assert.Equal(5, _tracker.TakeOutput(true).Credits.Single().Seconds);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void WallClockJump_CreditsMonotonicTimeOnly(int hours)
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        _time.AdvanceMonotonic(TimeSpan.FromSeconds(5));
        _time.WallNow += TimeSpan.FromHours(hours);
        _tracker.Advance();

        Assert.Equal(5, _tracker.TakeOutput(true).Credits.Sum(c => c.Seconds));
    }

    [Fact]
    public void TimeZoneChange_CreditsMonotonicTimeOnly()
    {
        var time = new ManualTimeProvider(Start, TimeZoneInfo.CreateCustomTimeZone("UTC+14", TimeSpan.FromHours(14), "x", "x"));
        var tracker = new UsageTracker(time, _logger);
        tracker.ObserveApps(2, Kid, [App(Notepad)]);
        tracker.SetActivity(_ => true);
        time.Advance(TimeSpan.FromSeconds(5));
        tracker.Advance();

        Assert.Equal([new UsageCredit(Kid, Notepad, Oct8, 5)], tracker.TakeOutput(true).Credits);
    }

    [Fact]
    public void Fractions_CarriedToNextTick()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        _time.Advance(TimeSpan.FromSeconds(2.5));
        _tracker.Advance();
        var first = _tracker.TakeOutput(true).Credits.Single().Seconds;
        _time.Advance(TimeSpan.FromSeconds(2.5));
        _tracker.Advance();

        Assert.Equal((2, 3), (first, _tracker.TakeOutput(true).Credits.Single().Seconds));
    }

    [Fact]
    public void TwoAccounts_Separate()
    {
        Observe(2, Kid, Notepad);
        Observe(3, Kid2, Notepad);
        _tracker.SetActivity(sid => sid == Kid);

        Assert.Equal([new UsageCredit(Kid, Notepad, Oct7, 5)], Tick().Credits);
    }

    [Fact]
    public void AlreadyRunningApps_CountedFromObservation()
    {
        _time.Advance(TimeSpan.FromMinutes(10));
        Observe(2, Kid, Notepad);
        Activate(Kid);

        Assert.Equal(5, Tick().Credits.Single().Seconds);
    }

    [Fact]
    public void StopAccount_EndsInstancesWithReason()
    {
        Observe(2, Kid, Notepad);
        _tracker.TakeOutput(false);

        _tracker.StopAccount(Kid, EndReasons.MonitoringStopped);

        var ended = Assert.IsType<InstanceEnded>(Assert.Single(_tracker.TakeOutput(false).Instances));
        Assert.Equal(EndReasons.MonitoringStopped, ended.Reason);
    }

    [Fact]
    public void StopAccount_GoneAppEndsAsClosedAtItsTime()
    {
        Observe(2, Kid, Notepad);
        Observe(2, Kid);
        _time.Advance(TimeSpan.FromSeconds(2));
        _tracker.TakeOutput(false);

        _tracker.StopAccount(Kid, EndReasons.MonitoringStopped);

        var ended = Assert.IsType<InstanceEnded>(Assert.Single(_tracker.TakeOutput(false).Instances));
        Assert.Equal((EndReasons.Closed, Start), (ended.Reason, ended.EndedUtc));
    }

    [Fact]
    public void StopAccount_LaterReportOfOtherSessionKept()
    {
        Observe(2, Kid, Notepad);
        Observe(3, Kid2, Paint);
        _tracker.TakeOutput(false);

        _tracker.StopAccount(Kid, EndReasons.MonitoringStopped);
        Activate(Kid2);

        Assert.Equal(Paint, Tick().Credits.Single().ProgramPath);
    }

    [Fact]
    public void RemoveSession_EndsWithSessionEnded()
    {
        Observe(2, Kid, Notepad);
        _tracker.TakeOutput(false);

        _tracker.RemoveSession(2);
        _tracker.RemoveSession(9);

        Assert.Equal(EndReasons.SessionEnded, Assert.IsType<InstanceEnded>(Assert.Single(_tracker.TakeOutput(false).Instances)).Reason);
    }

    [Fact]
    public void RemoveSession_AppStillOpenInOtherSession_Continues()
    {
        Observe(2, Kid, Notepad);
        Observe(4, Kid, Notepad);
        _tracker.TakeOutput(false);

        _tracker.RemoveSession(2);

        Assert.Empty(_tracker.TakeOutput(false).Instances);
    }

    [Fact]
    public void ObserveApps_SessionReusedByOtherUser_PreviousUserEnded()
    {
        Observe(2, Kid, Notepad);
        _tracker.TakeOutput(false);

        Observe(2, Kid2, Paint);

        var events = _tracker.TakeOutput(false).Instances;
        Assert.Equal((EndReasons.SessionEnded, Kid2), (((InstanceEnded)events[0]).Reason, events[1].AccountSid));
    }

    [Fact]
    public void StopAll_EndsEverythingWithServiceStopping()
    {
        Observe(2, Kid, Notepad);
        Observe(3, Kid2, Paint);
        _tracker.TakeOutput(false);

        _tracker.StopAll(EndReasons.ServiceStopping);

        Assert.All(_tracker.TakeOutput(false).Instances, i => Assert.Equal(EndReasons.ServiceStopping, ((InstanceEnded)i).Reason));
    }

    [Fact]
    public void TakeOutput_Tick_SeenForOpenInstancesOnly()
    {
        Observe(2, Kid, Notepad, Paint);
        var keys = _tracker.TakeOutput(false).Instances.Select(i => i.InstanceKey).ToList();
        Observe(2, Kid, Notepad);
        _time.Advance(TimeSpan.FromSeconds(1));

        var seen = _tracker.TakeOutput(true).Seen;

        Assert.Equal([new InstanceSeen(keys[0], Start.AddSeconds(1))], seen);
    }

    [Fact]
    public void TakeOutput_NoTick_NoUsage()
    {
        Observe(2, Kid, Notepad);
        Activate(Kid);
        _time.Advance(TimeSpan.FromSeconds(5));
        _tracker.Advance();

        var output = _tracker.TakeOutput(false);

        Assert.Equal((0, 0), (output.Credits.Count, output.Seen.Count));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _tracker.SetActivity(null!));
        Assert.ThrowsAny<ArgumentException>(() => _tracker.ObserveApps(2, " ", []));
        Assert.Throws<ArgumentNullException>(() => _tracker.ObserveApps(2, Kid, null!));
        Assert.ThrowsAny<ArgumentException>(() => _tracker.StopAccount(" ", "x"));
        Assert.ThrowsAny<ArgumentException>(() => _tracker.StopAccount(Kid, " "));
    }

    private static ObservedApp App(string path) => new(path, Path.GetFileName(path), path == Notepad ? "Editor" : "Paint", [1]);

    private void Observe(int session, string sid, params string[] paths) => _tracker.ObserveApps(session, sid, [.. paths.Select(App)]);

    private void Activate(string sid)
    {
        _active.Add(sid);
        _tracker.SetActivity(_active.Contains);
    }

    private TrackerOutput Tick()
    {
        _time.Advance(TimeSpan.FromSeconds(5));
        _tracker.Advance();
        return _tracker.TakeOutput(true);
    }
}
