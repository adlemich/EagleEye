using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class BreakTimeGateTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";
    private const string Paint = @"C:\Windows\System32\mspaint.exe";
    private const string Explorer = @"C:\Windows\explorer.exe";

    // 2026-10-12 is a Monday; Berlin is UTC+2 (summer time) that day.
    private static readonly BreakTimeEntryDto Evening = new(3, true, 1200, 1439, BreakTimeDays.All);
    private static readonly BreakTimeEntryDto Morning = new(4, true, 0, 420, BreakTimeDays.All);

    private readonly FakeTimeProvider _time = new(Utc(20, 10));
    private readonly Mock<IBreakTimeService> _rules = new();
    private readonly Mock<IProcessTable> _table = new();
    private readonly Mock<IBlockedStartRunner> _runner = new();
    private readonly List<BlockedStart> _started = [];
    private readonly List<Mock<IBlockedStartHandle>> _handles = [];
    private readonly TestLogger<BreakTimeGate> _logger = new();
    private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly BreakTimeGate _gate;
    private RulesSnapshot _snapshot = RulesSnapshot.Empty;

    public BreakTimeGateTests()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _rules.SetupGet(r => r.Current).Returns(() => _snapshot);
        _table.Setup(t => t.Snapshot(It.IsAny<int>())).Returns([]);
        _runner.Setup(r => r.Start(It.IsAny<BlockedStart>())).Returns<BlockedStart>(start =>
        {
            _started.Add(start);
            var handle = new Mock<IBlockedStartHandle>();
            _handles.Add(handle);
            return handle.Object;
        });
        _gate = new BreakTimeGate(_rules.Object, _table.Object, _runner.Object, new EnforcementIgnoreList(@"C:\Windows"), _time, _logger);
        SetRules(Kid, Evening);
    }

    [Fact]
    public void Screen_NoActiveEntry_ReportUnchangedNothingRead()
    {
        SetRules(Kid, Evening with { IsActive = false });
        var report = Report(App(Notepad, (10, Ft(20, 5))));

        Assert.Same(report, Screen(report));
        _table.VerifyNoOtherCalls();
        _runner.VerifyNoOtherCalls();
    }

    [Fact]
    public void Screen_FirstProcessCreatedInBreak_BlockedAndRemoved()
    {
        Screen(ReportRun(1));
        var result = Screen(ReportRun(1, App(Notepad, (10, Ft(20, 5))), App(Paint, (11, Ft(19, 0)))));

        Assert.Equal([Paint], result.Apps.Select(a => a.ProgramPath));
        var start = Assert.Single(_started);
        Assert.Equal(
            (2, Kid, "kid1", Notepad, "notepad.exe", "Editor", BlockedStartTexts.AppStartTrigger, Evening, Utc(20, 5), new DateTime(2026, 10, 12, 20, 5, 0), Utc(20, 10)),
            (start.SessionId, start.AccountSid, start.UserName, start.ProgramPath, start.ProcessName, start.DisplayName, start.Trigger, start.Entry, start.StartedUtc, start.StartedLocal, start.DetectedUtc));
        Assert.Equal([new ObservedProcess(10, Ft(20, 5))], start.Targets);
        Assert.Equal(BreakTimeRulesDefaults.Text, start.DisplayText);
    }

    [Fact]
    public void Screen_FirstReportOfAnAgentRun_TriggerFoundAtAgentStart()
    {
        Screen(ReportRun(7, App(Notepad, (10, Ft(20, 5)))));

        Assert.Equal(BlockedStartTexts.AgentStartTrigger, _started.Single().Trigger);
    }

    [Fact]
    public void Screen_AgentRestarted_AgainFoundAtAgentStart()
    {
        Screen(ReportRun(1));
        Screen(ReportRun(2, App(Notepad, (10, Ft(20, 5)))));

        Assert.Equal(BlockedStartTexts.AgentStartTrigger, _started.Single().Trigger);
    }

    [Fact]
    public void Screen_FirstProcessBeforeTheBreak_SlowWindowAllowed()
    {
        var report = Report(App(Notepad, (10, Ft(19, 59, 59)), (12, Ft(20, 0, 30))));

        Assert.Same(report, Screen(report));
        _table.VerifyNoOtherCalls();
    }

    [Fact]
    public void Screen_OlderProcessOfThePathInTheTable_Allowed()
    {
        // A program waiting in the notification area since before the break opens its window (AC-25).
        _table.Setup(t => t.Snapshot(2)).Returns(
        [
            new ProcessRow(9, 1, Ft(18, 0), 2, Kid, Notepad.ToUpperInvariant()),
            new ProcessRow(8, 1, Ft(17, 0), 2, Kid2, Notepad),
            new ProcessRow(7, 1, Ft(17, 0), 2, Kid, Paint),
        ]);

        var result = Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        Assert.Single(result.Apps);
        Assert.Empty(_started);
    }

    [Fact]
    public void Screen_OlderProcessInAnotherBreak_BlockedWithThatEntry()
    {
        SetRules(Kid, Evening, Morning);
        _table.Setup(t => t.Snapshot(2)).Returns([new ProcessRow(9, 1, Ft(3, 0), 2, Kid, Notepad)]);

        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        Assert.Equal((Morning, Utc(3, 0)), (_started.Single().Entry, _started.Single().StartedUtc));
    }

    [Fact]
    public void Screen_ProcessTableFails_ReportedProcessesDecide()
    {
        var failure = new InvalidOperationException("snapshot");
        _table.Setup(t => t.Snapshot(2)).Throws(failure);

        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        Assert.Single(_started);
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public void Screen_OpenApp_NewProcessesAreNotAStart()
    {
        _open.Add(Notepad);
        var report = Report(App(Notepad, (10, Ft(19, 0)), (12, Ft(20, 5))));

        Assert.Same(report, Screen(report));
        Assert.Empty(_started);
    }

    [Fact]
    public void Screen_AppBeingClosed_NewProcessesJoinTheSequenceAndStayExcluded()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        var result = Screen(Report(App(Notepad, (10, Ft(20, 5)), (12, Ft(20, 6)))));
        Screen(Report(App(Notepad, (10, Ft(20, 5)), (12, Ft(20, 6)))));

        Assert.Empty(result.Apps);
        Assert.Single(_started);
        _handles[0].Verify(h => h.AddProcesses(It.Is<IReadOnlyList<ObservedProcess>>(p => p.Single() == new ObservedProcess(12, Ft(20, 6)))), Times.Once);
    }

    [Fact]
    public void Screen_AfterTheSequenceEnded_NewStartBlockedAgain()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));
        _handles[0].SetupGet(h => h.IsCompleted).Returns(true);

        Screen(Report(App(Notepad, (20, Ft(20, 9)))));

        Assert.Equal(2, _started.Count);
    }

    [Fact]
    public void Screen_SequenceEndedAndEntrySwitchedOff_PassesUnchanged()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));
        _handles[0].SetupGet(h => h.IsCompleted).Returns(true);
        SetRules(Kid, Evening with { IsActive = false });
        var report = Report(App(Notepad, (20, Ft(20, 9))));

        Assert.Same(report, Screen(report));
    }

    [Fact]
    public void Screen_BlockedAppButEntrySwitchedOff_OtherAppsPass()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));
        SetRules(Kid, Evening with { IsActive = false });

        var result = Screen(Report(App(Notepad, (10, Ft(20, 5))), App(Paint, (11, Ft(20, 8)))));

        Assert.Equal([Paint], result.Apps.Select(a => a.ProgramPath));
    }

    [Fact]
    public void Screen_StartInBreakDetectedAfterTheBreak_StillBlocked()
    {
        _time.SetUtcNow(Utc(22, 30).AddHours(2)); // 00:30 local, the break 20:00–23:59 is over.

        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        Assert.Single(_started);
    }

    [Theory]
    [InlineData(Explorer)]
    [InlineData(@"C:\Windows\System32\ApplicationFrameHost.exe")]
    public void Screen_IgnoreList_NeverBlocked(string path)
    {
        var report = Report(App(path, (10, Ft(20, 5))));

        Assert.Same(report, Screen(report));
    }

    [Fact]
    public void Screen_AppWithoutProcesses_Kept()
    {
        var report = Report(new ObservedApp(Notepad, "notepad.exe", "Editor", []));

        Assert.Same(report, Screen(report));
    }

    [Fact]
    public void Screen_OtherAccount_NotAffected()
    {
        var report = new AppsObserved(3, Kid2, [App(Notepad, (10, Ft(20, 5)))], 1);

        Assert.Same(report, _gate.Screen(report, "kid2", _ => false));
    }

    [Fact]
    public void Screen_SessionReusedByAnotherAccount_StateReset()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));
        SetRules(Kid2, Evening);

        _gate.Screen(new AppsObserved(2, Kid2, [App(Notepad, (10, Ft(20, 5)))], 1), "kid2", _ => false);

        Assert.Equal(2, _started.Count);
    }

    [Fact]
    public void RemoveSession_ForgetsTheBlockedApps()
    {
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        _gate.RemoveSession(2);
        Screen(Report(App(Notepad, (10, Ft(20, 5)))));

        Assert.Equal(2, _started.Count);
    }

    [Fact]
    public void OnTick_LogsBeginAndEnd()
    {
        var controlled = new Dictionary<string, string> { [Kid] = "kid1" };
        _gate.OnTick(controlled);
        _gate.OnTick(controlled);
        _time.SetUtcNow(Utc(22, 0).AddHours(2));
        _gate.OnTick(controlled);
        _gate.OnTick(controlled);

        Assert.Equal(
            ["Break time of account kid1 began: 20:00–23:59 (Mo Tu We Th Fr Sa Su).", "Break time of account kid1 ended: 20:00–23:59 (Mo Tu We Th Fr Sa Su)."],
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void OnTick_OverlappingEntries_SwitchLogsEndAndBegin()
    {
        var early = new BreakTimeEntryDto(1, true, 1080, 1230, BreakTimeDays.All);
        SetRules(Kid, early, Evening);
        var controlled = new Dictionary<string, string> { [Kid] = "kid1" };
        _gate.OnTick(controlled);
        _time.SetUtcNow(Utc(20, 30));
        _gate.OnTick(controlled);

        Assert.Equal(
            ["Break time of account kid1 began: 18:00–20:30 (Mo Tu We Th Fr Sa Su).", "Break time of account kid1 ended: 18:00–20:30 (Mo Tu We Th Fr Sa Su).", "Break time of account kid1 began: 20:00–23:59 (Mo Tu We Th Fr Sa Su)."],
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void OnTick_AccountNoLongerControlled_ForgottenWithoutLog()
    {
        _gate.OnTick(new Dictionary<string, string> { [Kid] = "kid1" });
        _gate.OnTick(new Dictionary<string, string>());
        _gate.OnTick(new Dictionary<string, string> { [Kid] = "kid1" });

        Assert.Equal(2, _logger.Messages(LogLevel.Information).Count(m => m.Contains("began", StringComparison.Ordinal)));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _gate.Screen(null!, "kid1", _ => false));
        Assert.Throws<ArgumentNullException>(() => _gate.Screen(Report(), null!, _ => false));
        Assert.Throws<ArgumentNullException>(() => _gate.Screen(Report(), "kid1", null!));
        Assert.Throws<ArgumentNullException>(() => _gate.OnTick(null!));
    }

    private AppsObserved Screen(AppsObserved report) => _gate.Screen(report, "kid1", _open.Contains);

    private void SetRules(string sid, params BreakTimeEntryDto[] entries) =>
        _snapshot = _snapshot.With(sid, new AccountRules([.. entries], null));

    private static AppsObserved Report(params ObservedApp[] apps) => ReportRun(1, apps);

    private static AppsObserved ReportRun(long run, params ObservedApp[] apps) => new(2, Kid, apps, run);





    private static ObservedApp App(string path, params (int Pid, long Created)[] processes) =>
        new(path, Path.GetFileName(path), path == Notepad ? "Editor" : "App", [.. processes.Select(p => new ObservedProcess(p.Pid, p.Created))]);

    /// <summary>A UTC moment on Monday 2026-10-12 given as Berlin local time (UTC+2).</summary>
    private static DateTimeOffset Utc(int localHour, int minute, int second = 0) =>
        new DateTimeOffset(2026, 10, 12, localHour, minute, second, TimeSpan.FromHours(2)).ToUniversalTime();

    private static long Ft(int localHour, int minute, int second = 0) => Utc(localHour, minute, second).ToFileTime();

    private static class BreakTimeRulesDefaults
    {
        public const string Text = EagleEye.Shared.Constants.BreakTimeRules.DefaultDisplayText;
    }
}
