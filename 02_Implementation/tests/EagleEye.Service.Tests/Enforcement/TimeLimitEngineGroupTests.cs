using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Groups;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.Tests.Statistics;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

/// <summary>US-008 Decision 6 as scenarios (AC-24 to AC-34): group "Test" = Notepad + Paint. 2026-10-12 is a Monday.</summary>
public sealed class TimeLimitEngineGroupTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Notepad = @"C:\Apps\notepad.exe";
    private const string Paint = @"C:\Apps\mspaint.exe";
    private const long Test = 7;

    private readonly FakeTimeProvider _time = new(Local(10, 0));
    private readonly Mock<IBreakTimeService> _breaks = new();
    private readonly Mock<IAppRuleService> _appRules = new();
    private readonly GroupsView _groups = new();
    private readonly Mock<IWarningSettingsService> _warnings = new();
    private readonly Mock<IUsageRepository> _usage = new();
    private readonly Mock<ICloseSequenceRunner> _runner = new();
    private readonly Mock<IKidMessenger> _messenger = new();
    private readonly Mock<IOverlayPublisher> _overlay = new();
    private readonly List<RemainingTimeDto> _pushes = [];
    private readonly SessionAppsView _sessions = new();
    private readonly TestLogger<EnforcementLog> _logger = new();
    private readonly TodayUsageLedger _ledger;
    private readonly TimeLimitEngine _engine;
    private readonly List<KidMessageDto> _messages = [];
    private readonly List<CloseRequest> _closes = [];
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _controlled = new() { [Kid] = "kid1" };
    private RulesSnapshot _breakRules = RulesSnapshot.Empty;
    private AppRulesSnapshot _apps = AppRulesSnapshot.Empty;

    public TimeLimitEngineGroupTests()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _breaks.SetupGet(b => b.Current).Returns(() => _breakRules);
        _appRules.SetupGet(a => a.Current).Returns(() => _apps);
        _warnings.SetupGet(w => w.Current).Returns(new WarningSettings(3, 1));
        _usage.Setup(u => u.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _usage.Setup(u => u.GetSpansAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _messenger.Setup(m => m.ShowAsync(It.IsAny<int>(), It.IsAny<KidMessageDto>(), It.IsAny<CancellationToken>()))
            .Callback<int, KidMessageDto, CancellationToken>((_, m, _) => _messages.Add(m))
            .ReturnsAsync(KidMessageStates.Shown);
        _overlay.Setup(o => o.Publish(It.IsAny<int>(), It.IsAny<RemainingTimeDto>()))
            .Callback<int, RemainingTimeDto>((_, m) => _pushes.Add(m))
            .Returns(KidMessageStates.ShownInOverlay);
        _runner.Setup(r => r.Start(It.IsAny<CloseRequest>())).Returns<CloseRequest>(request =>
        {
            _closes.Add(request);
            return Mock.Of<ICloseSequenceHandle>();
        });
        _ledger = new TodayUsageLedger(_usage.Object, _appRules.Object, _time, new TestLogger<TodayUsageLedger>());
        _ledger.EnsureLoadedAsync([Kid], CancellationToken.None).GetAwaiter().GetResult();
        _engine = new TimeLimitEngine(
            _breaks.Object, _appRules.Object, _groups, _warnings.Object, _ledger, _sessions, new CloseRegistry(), _runner.Object, _messenger.Object, _overlay.Object,
            new EnforcementIgnoreList(@"C:\Windows"), new EnforcementLog(_logger, new InstanceLogLimiter(_time)), _time);
        SetOwnRule(1, Notepad, 600);
        SetOwnRule(2, Paint, 600);
        _groups.Current = AppGroupsSnapshot.Empty.WithGroup(Test, "Test").WithMember(Notepad, Test).WithMember(Paint, Test);
    }

    [Fact]
    public void OwnersExample_WarningsOncePerGroup_OneLine_BothClosedWithOneMessage()
    {
        // AC-26, AC-27: "Test" 0:05, early 3 / final 1; Notepad 3 min alone, then Notepad and Paint 2 min together.
        SetGroupRule(5);
        Run(Notepad);
        Evaluate();
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Test", 300, OverlayLevel.Normal, true), _pushes[^1].Lines.Single());

        Use(120, Notepad);
        Evaluate();
        Assert.Equal((OverlaySignal.Early, 180), (_pushes[^1].Signal, _pushes[^1].Lines.Single().Seconds));

        Use(60, Notepad);
        Run(Notepad, Paint);
        Evaluate();
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Test", 120, OverlayLevel.Early, true), _pushes[^1].Lines.Single());
        Use(60, Notepad, Paint);
        Evaluate();
        Assert.Equal(OverlaySignal.Final, _pushes[^1].Signal);

        Use(60, Notepad, Paint);
        Evaluate();

        Assert.Equal([OverlaySignal.Early, OverlaySignal.Final, OverlaySignal.Expired], Signals());
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Test", 0, OverlayLevel.Expired, false), _pushes[^1].Lines.Single());
        Assert.Equal(new KidMessageDto(KidMessageReason.AppBudgetUsedUp, null, "Test", null, null), _messages.Single());
        Assert.Equal([Paint, Notepad], _closes.Select(c => c.ProgramPath).Order());
        Assert.All(_closes, c => Assert.Equal((null, true), (c.Message, c.SharedMessageState is not null)));
        Assert.All(_closes, c => Assert.Equal(new AppRuleAtClose(5, BreakTimeDays.All, 300, Test, "Test"), c.Rule));
        Assert.Contains("Warning: account kid1, group Test (Editor), early warning, 3 min left (budget); shown in overlay.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void OwnRuleIgnoredInTheGroup()
    {
        // AC-24: Paint's own rule 0:00, "Test" 1:00 and today ticked → Paint runs.
        SetOwnRule(2, Paint, 0);
        SetGroupRule(60);
        Run(Paint);

        Evaluate();

        Assert.Empty(Signals());
        Assert.Empty(_closes);
    }

    [Fact]
    public void GroupNotAllowedToday_OneMemberRunning_ClosedWithItsOwnMessage()
    {
        SetGroupRule(60, BreakTimeDays.Sunday);
        Run(Paint);

        Evaluate();

        var close = _closes.Single();
        Assert.Equal((CloseReasons.NotAllowedToday, "Test"), (close.Reason, close.Message!.AppName));
    }

    [Fact]
    public void RunningAppMovedIntoAnExhaustedGroup_GraceWithTheGroupName_ThenClosed()
    {
        // AC-32 example 1.
        _groups.Current = AppGroupsSnapshot.Empty.WithGroup(Test, "Test").WithMember(Notepad, Test);
        SetGroupRule(10);
        Run(Paint);
        Use(600, Notepad);
        Evaluate();
        Assert.Empty(Signals());

        _groups.Current = _groups.Current.WithMember(Paint, Test);
        Evaluate();
        Assert.Equal((OverlaySignal.Final, new OverlayLineDto(OverlayLineKind.Group, "Test", 60, OverlayLevel.Final, true)), (_pushes[^1].Signal, _pushes[^1].Lines.Single()));
        Assert.Empty(_messages);

        Advance(30);
        Evaluate();
        Assert.Empty(_closes);

        Advance(31);
        Evaluate();
        Assert.Equal((BlockedStartTexts.GraceEndedTrigger, true), (_closes.Single().Trigger, _closes.Single().AfterGrace));
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.StartsWith("Grace period: account kid1, group Test (App): the rules changed (budget)", StringComparison.Ordinal));
    }

    [Fact]
    public void GroupDeletedWhileRunning_OwnRuleZero_GraceNamesTheApp()
    {
        // AC-32 example 2.
        SetOwnRule(2, Paint, 0);
        SetGroupRule(60);
        Run(Paint);
        Evaluate();

        _groups.Current = _groups.Current.WithoutGroup(Test);
        Evaluate();

        Assert.Equal((OverlaySignal.Final, new OverlayLineDto(OverlayLineKind.App, "App", 60, OverlayLevel.Final, true)), (_pushes[^1].Signal, _pushes[^1].Lines.Single()));
    }

    [Fact]
    public void Rename_NoGraceNoMessage()
    {
        // AC-34.
        SetGroupRule(3);
        Run(Notepad);
        Evaluate();
        _pushes.Clear();

        _groups.Current = _groups.Current.WithGroup(Test, "Gaming");
        Evaluate();

        Assert.Empty(Signals());
        Assert.Equal("Gaming", _pushes[^1].Lines.Single().Name);
    }

    [Fact]
    public void NotRunningAppMovedIn_ShortensTheRunningMember_GraceForTheRunningApp()
    {
        // AC-34 example: Paint runs in "Test" (1:00, 30 min used); Notepad, not running, used 40 min alone; Notepad joins.
        _groups.Current = AppGroupsSnapshot.Empty.WithGroup(Test, "Test").WithMember(Paint, Test);
        SetGroupRule(60);
        Use(2400, Notepad);
        Run(Paint);
        Use(1800, Paint);
        Evaluate();
        Assert.Empty(Signals());

        _groups.Current = _groups.Current.WithMember(Notepad, Test);
        Evaluate();
        Assert.Equal([OverlaySignal.Final], Signals());

        Advance(61);
        Evaluate();
        Assert.Equal(Paint, _closes.Single().ProgramPath);
    }

    [Fact]
    public void MoveLeavingMoreThanTheEarlyLevel_NoMessage()
    {
        _groups.Current = AppGroupsSnapshot.Empty.WithGroup(Test, "Test").WithMember(Paint, Test);
        SetGroupRule(60);
        Use(600, Notepad);
        Run(Paint);
        Evaluate();

        _groups.Current = _groups.Current.WithMember(Notepad, Test);
        Evaluate();

        Assert.Empty(Signals());
    }

    [Fact]
    public void GraceSharedByAMemberStartedLater_ThenBothClosedWithOneMessage()
    {
        SetGroupRule(60);
        Run(Paint);
        Evaluate();
        SetGroupRule(0);
        Evaluate();
        Assert.Equal([OverlaySignal.Final], Signals());

        Run(Paint, Notepad);
        Advance(30);
        Evaluate();
        Assert.Equal([OverlaySignal.Final], Signals());
        Assert.Equal(30, _pushes[^1].Lines.Single().Seconds);
        Assert.Empty(_closes);

        Advance(31);
        Evaluate();
        Assert.Equal(2, _closes.Count);
        Assert.Single(_messages);
    }

    [Fact]
    public void ChangeWithinTheEarlyLevel_EarlyAtOnce_NoGrace()
    {
        // AC-33: 50 min used of 1:00, the parent cuts to 0:52 → 2 min left: early at once, no grace.
        SetGroupRule(60);
        Run(Notepad);
        Use(3000, Notepad);
        Evaluate();
        Assert.Empty(Signals());

        SetGroupRule(52);
        Evaluate();

        Assert.Equal([OverlaySignal.Early], Signals());
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("group Test (Editor), early warning, 2 min left (budget)", StringComparison.Ordinal));
        Assert.Empty(_closes);
    }

    [Fact]
    public void RaisedBudget_WarningNotShownAgain()
    {
        // AC-33: a change that gives more time does not bring back warnings already shown.
        SetGroupRule(60);
        Run(Notepad);
        Use(3480, Notepad);
        Evaluate();
        _pushes.Clear();

        SetGroupRule(59, BreakTimeDays.All, 2);
        Evaluate();

        Assert.Empty(Signals());
        Assert.Equal(OverlayLevel.Early, _pushes[^1].Lines.Single().Level);
        Assert.Empty(_closes);
    }

    [Fact]
    public void BreakBeforeTheGroupEnd_BreakWarningsOnly()
    {
        // AC-28.
        _breakRules = _breakRules.With(Kid, new AccountRules([new BreakTimeEntryDto(3, true, 602, 700, BreakTimeDays.All)], null));
        SetGroupRule(5);
        Run(Notepad, Paint);

        Evaluate();

        Assert.Equal([OverlaySignal.Early], Signals());
        Assert.Contains(new OverlayLineDto(OverlayLineKind.Group, "Test", 300, OverlayLevel.Normal, true), _pushes[^1].Lines);
        Assert.Contains(new OverlayLineDto(OverlayLineKind.Break, null, 120, OverlayLevel.Early, true), _pushes[^1].Lines);
    }

    [Fact]
    public void ClosedGroup_ExpiringLineReplacedWhenAnotherMemberRunsAgain()
    {
        SetGroupRule(5);
        Run(Notepad);
        Use(300, Notepad);
        Evaluate();
        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Test", 0, OverlayLevel.Expired, false), _pushes[^1].Lines.Single());
        Evaluate();
        Assert.Equal(OverlayLevel.Expired, _pushes[^1].Lines.Single().Level);

        SetGroupRule(10);
        Run(Notepad, Paint);
        Evaluate();

        Assert.Equal(new OverlayLineDto(OverlayLineKind.Group, "Test", 300, OverlayLevel.Normal, true), _pushes[^1].Lines.Single());
    }

    private List<OverlaySignal> Signals() => [.. _pushes.Select(p => p.Signal).Where(s => s != OverlaySignal.None)];

    private void Evaluate() => _engine.Evaluate(_controlled, (_, path) => _running.Contains(path), new HashSet<int> { 2 });

    private void Run(params string[] paths)
    {
        _running.Clear();
        _running.UnionWith(paths);
        _sessions.Update(2, Kid, [.. paths.Select((p, i) => new ObservedApp(p, Path.GetFileName(p), p == Notepad ? "Editor" : "App", [new ObservedProcess(10 + i, Local(9, 0).ToFileTime())]))]);
    }

    /// <summary>The paths were active together for the last <paramref name="seconds"/> (credits and spans, as the tracker feeds them).</summary>
    private void Use(int seconds, params string[] paths)
    {
        var start = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromSeconds(seconds));
        var day = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        _ledger.Apply([.. paths.Select(p => new UsageCredit(Kid, p, day, seconds))]);
        _ledger.ApplySpans([.. paths.Select(p => new SpanUpdate(Kid, p, day, start, _time.GetUtcNow()))]);
    }

    private void Advance(int seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    private void SetOwnRule(long appId, string path, int budget) =>
        _apps = _apps.With(Kid, _apps.For(Kid).WithApp(new AppRuleEntry(appId, path, path == Notepad ? "Editor" : "App", budget, BreakTimeDays.All)));

    private void SetGroupRule(int budget, BreakTimeDays days = BreakTimeDays.All, int extraMinutes = 0) =>
        _groups.Current = _groups.Current.WithRule(Test, Kid, new LimitRule(budget + extraMinutes, days));

    private static DateTimeOffset Local(int hour, int minute, int second = 0) =>
        new DateTimeOffset(2026, 10, 12, hour, minute, second, TimeSpan.FromHours(2)).ToUniversalTime();
}
