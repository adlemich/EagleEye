using EagleEye.Service.Communication;
using EagleEye.Service.Data;
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

/// <summary>US-006 Decisions 5 to 8 as scenarios. 2026-10-12 is a Monday; Berlin is UTC+2 that day.</summary>
public sealed class TimeLimitEngineTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";
    private const string Paint = @"C:\Apps\paint.exe";
    private const string Explorer = @"C:\Windows\explorer.exe";
    private static readonly BreakTimeEntryDto Evening = new(3, true, 1200, 1439, BreakTimeDays.All);

    private readonly FakeTimeProvider _time = new(Local(10, 0));
    private readonly Mock<IBreakTimeService> _breaks = new();
    private readonly Mock<IAppRuleService> _appRules = new();
    private readonly Mock<IWarningSettingsService> _warnings = new();
    private readonly Mock<IUsageRepository> _usage = new();
    private readonly Mock<ICloseSequenceRunner> _runner = new();
    private readonly Mock<IKidMessenger> _messenger = new();
    private readonly SessionAppsView _sessions = new();
    private readonly CloseRegistry _registry = new();
    private readonly TestLogger<EnforcementLog> _logger = new();
    private readonly TodayUsageLedger _ledger;
    private readonly TimeLimitEngine _engine;
    private readonly List<(int Session, KidMessageDto Message)> _messages = [];
    private readonly List<(CloseRequest Request, Mock<ICloseSequenceHandle> Handle)> _closes = [];
    private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _controlled = new() { [Kid] = "kid1" };
    private RulesSnapshot _breakRules = RulesSnapshot.Empty;
    private AppRulesSnapshot _apps = AppRulesSnapshot.Empty;
    private WarningSettings _settings = new(5, null, 1, null);

    public TimeLimitEngineTests()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _breaks.SetupGet(b => b.Current).Returns(() => _breakRules);
        _appRules.SetupGet(a => a.Current).Returns(() => _apps);
        _warnings.SetupGet(w => w.Current).Returns(() => _settings);
        _usage.Setup(u => u.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _messenger.Setup(m => m.ShowAsync(It.IsAny<int>(), It.IsAny<KidMessageDto>(), It.IsAny<CancellationToken>()))
            .Callback<int, KidMessageDto, CancellationToken>((s, m, _) => _messages.Add((s, m)))
            .ReturnsAsync(KidMessageStates.Shown);
        _runner.Setup(r => r.Start(It.IsAny<CloseRequest>())).Returns<CloseRequest>(request =>
        {
            var handle = new Mock<ICloseSequenceHandle>();
            _closes.Add((request, handle));
            return handle.Object;
        });
        _ledger = new TodayUsageLedger(_usage.Object, _appRules.Object, _time, new TestLogger<TodayUsageLedger>());
        _ledger.EnsureLoadedAsync([Kid, Kid2], CancellationToken.None).GetAwaiter().GetResult();
        _engine = new TimeLimitEngine(
            _breaks.Object, _appRules.Object, _warnings.Object, _ledger, _sessions, _registry, _runner.Object, _messenger.Object,
            new EnforcementIgnoreList(@"C:\Windows"), new EnforcementLog(_logger, new InstanceLogLimiter(_time)), _time);
        SetRule(Kid, 1, Notepad, 3);
        SetRule(Kid, 2, Paint, 60);
    }

    // ---------- Budget: AC-22, AC-24, AC-25 ----------

    [Fact]
    public void Budget_EarlyAtOnceThenFinalThenClose()
    {
        Run(Notepad);

        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.AppEarlyWarning, null, "Editor", 3, null, null), _messages.Single().Message);

        Use(Notepad, 115);
        Evaluate();
        Assert.Single(_messages);

        Use(Notepad, 5);
        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.AppFinalWarning, null, "Editor", 1, null, null), _messages[^1].Message);

        Use(Notepad, 60);
        Evaluate();
        var close = _closes.Single().Request;
        Assert.Equal(
            (CloseKinds.RunningApp, CloseReasons.BudgetUsedUp, BlockedStartTexts.BudgetRanOutTrigger, false, new AppRuleAtClose(3, BreakTimeDays.All, 180)),
            (close.Kind, close.Reason, close.Trigger, close.AfterGrace, close.Rule));
        Assert.Equal(new KidMessageDto(KidMessageReason.AppBudgetUsedUp, null, "Editor", null, null, null), close.Message);
        Assert.False(close.RemovesFromReport);
        Assert.Equal(2, _messages.Count);
        Assert.Contains("Warning: account kid1, Editor (notepad.exe), early warning, 3 min left (budget); message shown.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void StartWithLessThanTheFinalLeft_FinalOnly_EarlyNeverAfter()
    {
        Use(Notepad, 140);
        Run(Notepad);

        Evaluate();
        Use(Notepad, 10);
        Evaluate();

        Assert.Equal(KidMessageReason.AppFinalWarning, _messages.Single().Message.Reason);
    }

    [Fact]
    public void AppRestartedTheSameDay_WarningNotShownAgain()
    {
        Run(Notepad);
        Evaluate();
        _sessions.Update(2, Kid, []);
        Evaluate();

        Run(Notepad, pid: 20);
        Evaluate();

        Assert.Single(_messages);
    }

    [Fact]
    public void ClosingApp_NotClosedTwice_StaleReportSkipped_NewProcessesClosedAgain()
    {
        Use(Notepad, 180);
        Run(Notepad);
        Evaluate();
        Evaluate();
        Assert.Single(_closes);

        _closes[0].Handle.SetupGet(h => h.IsCompleted).Returns(true);
        Evaluate();
        Assert.Single(_closes);

        Run(Notepad, pid: 30);
        Evaluate();
        Assert.Equal(2, _closes.Count);
    }

    [Fact]
    public void FirstSeenWithNothingLeft_ClosedAtOnceWithoutGrace()
    {
        SetRule(Kid, 1, Notepad, 0);
        Run(Notepad);

        Evaluate();

        Assert.Equal((CloseReasons.NotAllowedToday, BlockedStartTexts.RulesChangedTrigger), (_closes.Single().Request.Reason, _closes.Single().Request.Trigger));
        Assert.Equal(KidMessageReason.AppNotAllowedToday, _closes.Single().Request.Message!.Reason);
        Assert.Empty(_messages);
    }

    [Fact]
    public void NoEndWithinTheHorizon_Nothing()
    {
        Run(Paint);

        Evaluate();

        Assert.Empty(_messages);
        Assert.Empty(_closes);
    }

    // ---------- Exemptions: AC-26, AC-27 ----------

    [Fact]
    public void NotOpenIgnoredUncontrolledOrWithoutProcesses_NeverWarnedOrClosed()
    {
        SetRule(Kid, 1, Notepad, 0);
        SetRule(Kid2, 5, Paint, 0);
        _sessions.Update(2, Kid, [App(Notepad, 10, open: false), App(Explorer, 11), new ObservedApp(Paint, "paint.exe", "Paint", [])]);
        _sessions.Update(3, Kid2, [App(Paint, 12)]);
        _controlled = new Dictionary<string, string> { [Kid] = "kid1" };

        Evaluate();

        Assert.Empty(_closes);
        Assert.Empty(_messages);
    }

    [Fact]
    public void AppBeingClosedBySomethingElse_Skipped()
    {
        SetRule(Kid, 1, Notepad, 0);
        Run(Notepad);
        _registry.Add(2, Notepad, new CloseEntry(Mock.Of<ICloseSequenceHandle>(), true, []));

        Evaluate();

        Assert.Empty(_closes);
    }

    // ---------- Break times: AC-28 to AC-31 ----------

    [Fact]
    public void BreakBegins_WarningsOncePerSessionThenAllAppsClosedWithOneMessage()
    {
        SetBreaks(Kid, Evening);
        Run(Notepad, Paint);
        SetRule(Kid, 1, Notepad, 120);
        At(19, 54, 50);
        Evaluate();
        Assert.Empty(_messages);

        At(19, 55, 0);
        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakEarlyWarning, null, null, 5, 1200, null), _messages.Single().Message);

        At(19, 59, 0);
        Evaluate();
        Assert.Equal(KidMessageReason.BreakFinalWarning, _messages[^1].Message.Reason);

        At(20, 0, 2);
        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakTime, null, null, null, 1200, 1439), _messages[^1].Message);
        Assert.Equal(3, _messages.Count);
        Assert.Equal(2, _closes.Count);
        Assert.All(_closes, c => Assert.Equal(
            (CloseReasons.BreakTime, BlockedStartTexts.BreakBeganTrigger, (KidMessageDto?)null, Evening, (AppRuleAtClose?)null),
            (c.Request.Reason, c.Request.Trigger, c.Request.Message, c.Request.Entry, c.Request.Rule)));
        Assert.Same(_closes[0].Request.SharedMessageState, _closes[1].Request.SharedMessageState);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.StartsWith("Warning: account kid1, break time 20:00–23:59", StringComparison.Ordinal));
    }

    [Fact]
    public void StartShortlyBeforeABreak_EarlyThenFinal()
    {
        SetBreaks(Kid, Evening);
        At(19, 58, 0);
        Run(Paint);

        Evaluate();
        At(19, 59, 0);
        Evaluate();

        Assert.Equal([KidMessageReason.BreakEarlyWarning, KidMessageReason.BreakFinalWarning], _messages.Select(m => m.Message.Reason));
        Assert.Equal(2, _messages[0].Message.Minutes);
    }

    [Fact]
    public void BudgetEndsAfterTheBreak_OnlyBreakWarnings_BudgetFirst_OnlyAppWarnings()
    {
        SetBreaks(Kid, Evening);
        SetRule(Kid, 1, Notepad, 30);
        SetRule(Kid, 2, Paint, 2);
        At(19, 57, 0);
        Run(Notepad, Paint);

        Evaluate();

        Assert.Equal(KidMessageReason.BreakEarlyWarning, _messages.Single().Message.Reason);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("early warning, 2 min left (budget); message not shown (replaced in the same tick)", StringComparison.Ordinal));
    }

    // ---------- Grace periods: AC-32, AC-33 ----------

    [Fact]
    public void RuleChangedToZero_GraceOfTheFinalMinutesThenClose()
    {
        Run(Notepad);
        SetRule(Kid, 1, Notepad, 60);
        Evaluate();
        SetRule(Kid, 1, Notepad, 0);

        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.AppGracePeriod, null, "Editor", 1, null, null), _messages.Single().Message);
        Assert.Empty(_closes);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.StartsWith("Grace period: account kid1, Editor (notepad.exe): the rules changed (not allowed today); closes in 1 min at", StringComparison.Ordinal));

        Advance(59);
        Evaluate();
        Assert.Empty(_closes);

        Advance(1);
        Evaluate();
        var close = _closes.Single().Request;
        Assert.Equal((true, BlockedStartTexts.GraceEndedTrigger, CloseReasons.NotAllowedToday), (close.AfterGrace, close.Trigger, close.Reason));
        Assert.Single(_messages);
    }

    [Fact]
    public void ChangeLeaving20Seconds_StillTheFullFinalMinutes()
    {
        SetRule(Kid, 1, Notepad, 60);
        Run(Notepad);
        Evaluate();
        Use(Notepad, 160);

        SetRule(Kid, 1, Notepad, 3);
        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.AppGracePeriod, null, "Editor", 1, null, null), _messages.Single().Message);

        Use(Notepad, 50);
        Evaluate();
        Assert.Empty(_closes);

        Advance(10);
        Evaluate();
        Assert.True(_closes.Single().Request.AfterGrace);
    }
    [Fact]
    public void ChangeLeavingAtLeastTheFinalButWithinTheEarly_EarlyAtOnceNoGrace()
    {
        SetRule(Kid, 1, Notepad, 60);
        Run(Notepad);
        Evaluate();
        Use(Notepad, 60);

        SetRule(Kid, 1, Notepad, 4);
        Evaluate();

        Assert.Equal((KidMessageReason.AppEarlyWarning, 3), (_messages.Single().Message.Reason, _messages.Single().Message.Minutes));
    }

    [Fact]
    public void RaisedDuringTheGrace_GraceEndsWarningsNotReArmed()
    {
        SetRule(Kid, 1, Notepad, 60);
        Run(Notepad);
        Evaluate();
        SetRule(Kid, 1, Notepad, 0);
        Evaluate();

        SetRule(Kid, 1, Notepad, 3);
        Evaluate();
        Advance(120);
        Evaluate();
        Use(Notepad, 125);
        Evaluate();

        Assert.Single(_messages);
        Assert.Empty(_closes);
    }

    [Fact]
    public void ChangeMovingTheEndLater_NoGrace()
    {
        Use(Notepad, 170);
        Run(Notepad);
        Evaluate();

        SetRule(Kid, 1, Notepad, 4);
        Evaluate();

        Assert.Equal([KidMessageReason.AppFinalWarning], _messages.Select(m => m.Message.Reason));
    }

    [Fact]
    public void BreakNowInEffect_OneGraceMessagePerSessionThenCloseWithTheBreakText()
    {
        At(19, 0, 0);
        Run(Notepad, Paint);
        SetRule(Kid, 1, Notepad, 60);
        Evaluate();
        SetBreaks(Kid, Evening with { StartMinute = 1140 });

        Evaluate();
        Assert.Equal(new KidMessageDto(KidMessageReason.BreakGracePeriod, null, null, 1, 1140, null), _messages.Single().Message);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("begins at 19:00; 2 app(s) close in 1 min", StringComparison.Ordinal));

        Advance(60);
        Evaluate();
        Assert.Equal(2, _closes.Count);
        Assert.All(_closes, c => Assert.Equal((true, CloseReasons.BreakTime), (c.Request.AfterGrace, c.Request.Reason)));
        Assert.Equal(KidMessageReason.BreakTime, _messages[^1].Message.Reason);
    }

    [Fact]
    public void BreakMovedToBeginIn30Seconds_GraceOfTheFullMinute()
    {
        At(19, 0, 0);
        Run(Paint);
        Evaluate();
        SetBreaks(Kid, Evening with { StartMinute = 1141 });
        At(19, 0, 30);

        Evaluate();
        Advance(30);
        Evaluate();
        Assert.Empty(_closes);

        Advance(30);
        Evaluate();
        Assert.Single(_closes);
    }

    [Fact]
    public void BreakMovedWithinTheEarlyLevel_EarlyAtOnceNoGrace()
    {
        At(19, 0, 0);
        Run(Paint);
        Evaluate();

        SetBreaks(Kid, Evening with { StartMinute = 1143 });
        Evaluate();

        Assert.Equal((KidMessageReason.BreakEarlyWarning, 3), (_messages.Single().Message.Reason, _messages.Single().Message.Minutes));
    }

    [Fact]
    public void WarningLevelChange_NeverStartsAGrace_AppliesToUnshownWarnings()
    {
        Use(Notepad, 0);
        SetRule(Kid, 1, Notepad, 10);
        Run(Notepad);
        Evaluate();
        Assert.Empty(_messages);

        _settings = new WarningSettings(15, null, 10, null);
        Evaluate();

        Assert.Equal(KidMessageReason.AppFinalWarning, _messages.Single().Message.Reason);
    }

    [Fact]
    public void NotRunningAtTheChange_NoGraceAtTheNextStart()
    {
        SetRule(Kid, 1, Notepad, 60);
        Evaluate();
        SetRule(Kid, 1, Notepad, 0);
        Evaluate();

        Run(Notepad);
        Evaluate();

        Assert.Empty(_messages);
        Assert.Single(_closes);
    }

    [Fact]
    public void AccountLeavesControl_StateCleared()
    {
        SetRule(Kid, 1, Notepad, 60);
        Run(Notepad);
        Evaluate();
        SetRule(Kid, 1, Notepad, 0);
        Evaluate();
        Assert.Single(_messages);

        _controlled = [];
        Evaluate();
        _controlled = new Dictionary<string, string> { [Kid] = "kid1" };
        Evaluate();

        Assert.Equal(BlockedStartTexts.RulesChangedTrigger, _closes.Single().Request.Trigger);
        Assert.False(_closes.Single().Request.AfterGrace);
    }

    // ---------- Midnight (OQ-4) ----------

    [Fact]
    public void MidnightWithTheNextDayNotAllowed_WarningsThenClosedAtMidnight()
    {
        SetRule(Kid, 2, Paint, 1439, BreakTimeDays.Monday);
        At(23, 59, 30);
        Run(Paint);
        Evaluate();
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("final warning, 1 min left (midnight)", StringComparison.Ordinal));

        _time.SetUtcNow(Local(23, 59, 30).AddSeconds(35));
        Evaluate();

        var close = _closes.Single().Request;
        Assert.Equal((CloseReasons.NotAllowedToday, BlockedStartTexts.MidnightTrigger), (close.Reason, close.Trigger));
        Assert.Equal(KidMessageReason.AppNotAllowedToday, close.Message!.Reason);
    }

    // ---------- Guards ----------

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Evaluate(null!, (_, _) => true));
        Assert.Throws<ArgumentNullException>(() => _engine.Evaluate(_controlled, null!));
    }

    private void Evaluate() => _engine.Evaluate(_controlled, (_, path) => _open.Contains(path));

    private void Run(params string[] paths) => Run(paths, 10);

    private void Run(string path, int pid) => Run([path], pid);

    private void Run(string[] paths, int pid)
    {
        _sessions.Update(2, Kid, [.. paths.Select((p, i) => App(p, pid + i))]);
    }

    private ObservedApp App(string path, int pid, bool open = true)
    {
        if (open)
        {
            _open.Add(path);
        }

        return new ObservedApp(path, Path.GetFileName(path), path == Notepad ? "Editor" : "App", [new ObservedProcess(pid, Local(9, 0).ToFileTime())]);
    }

    private void Use(string path, long seconds)
    {
        _time.Advance(TimeSpan.FromSeconds(seconds));
        _ledger.Apply([new UsageCredit(Kid, path, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), seconds)]);
    }

    private void Advance(int seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    private void At(int hour, int minute, int second) => _time.SetUtcNow(Local(hour, minute, second));

    private void SetRule(string sid, long appId, string path, int budget, BreakTimeDays days = BreakTimeDays.All) =>
        _apps = _apps.With(sid, _apps.For(sid).WithApp(new AppRuleEntry(appId, path, path == Notepad ? "Editor" : "App", budget, days)));

    private void SetBreaks(string sid, params BreakTimeEntryDto[] entries) =>
        _breakRules = _breakRules.With(sid, new AccountRules([.. entries], null));

    private static DateTimeOffset Local(int hour, int minute, int second = 0) =>
        new DateTimeOffset(2026, 10, 12, hour, minute, second, TimeSpan.FromHours(2)).ToUniversalTime();
}
