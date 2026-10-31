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

public sealed class LimitSubjectTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Apps\notepad.exe";
    private const string Paint = @"C:\Apps\mspaint.exe";
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private readonly TodayUsageLedger _ledger;
    private readonly AccountAppRules _apps = AccountAppRules.Empty.WithApp(new AppRuleEntry(1, Notepad, "Editor", 45, BreakTimeDays.Monday));
    private readonly AppGroupsSnapshot _groups = AppGroupsSnapshot.Empty.WithGroup(7, "Test").WithMember(Paint, 7).WithRule(7, Kid, new LimitRule(60, BreakTimeDays.All));

    public LimitSubjectTests()
    {
        var time = new FakeTimeProvider(T0);
        time.SetLocalTimeZone(TestZones.Berlin);
        var usage = new Mock<IUsageRepository>();
        usage.Setup(u => u.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        usage.Setup(u => u.GetSpansAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ledger = new TodayUsageLedger(usage.Object, Mock.Of<IAppRuleService>(r => r.Current == AppRulesSnapshot.Empty), time, new TestLogger<TodayUsageLedger>());
        _ledger.EnsureLoadedAsync([Kid, Kid2], CancellationToken.None).GetAwaiter().GetResult();
        _ledger.Apply([new UsageCredit(Kid, Notepad, Today, 100)]);
        _ledger.ApplySpans([new SpanUpdate(Kid, Paint, Today, T0, T0.AddMinutes(10))]);
    }

    [Fact]
    public void Resolve_UngroupedApp_OwnRuleAndAppTime()
    {
        var subject = LimitSubjectResolver.Resolve(_ledger, Kid, Notepad, "Editor", _apps, _groups, Today);

        Assert.Equal((LimitSubjectKind.App, $"app|{Notepad.ToUpperInvariant()}", "Editor", (long?)null), (subject.Kind, subject.Key, subject.Name, subject.GroupId));
        Assert.Equal((new LimitRule(45, BreakTimeDays.Monday), 100L, Notepad), (subject.Rule, subject.UsedSeconds, subject.MemberPaths.Single()));
    }

    [Fact]
    public void Resolve_UngroupedAppWithoutRule_DefaultRule()
    {
        Assert.Equal(LimitRule.Default, LimitSubjectResolver.Resolve(_ledger, Kid, @"C:\x.exe", "X", _apps, _groups, Today).Rule);
    }

    [Fact]
    public void Resolve_GroupedApp_GroupRuleGroupTimeAndName()
    {
        var subject = LimitSubjectResolver.Resolve(_ledger, Kid, Paint.ToUpperInvariant(), "Paint", _apps, _groups, Today);

        Assert.Equal((LimitSubjectKind.Group, "group|7", "Test", (long?)7), (subject.Kind, subject.Key, subject.Name, subject.GroupId));
        Assert.Equal((new LimitRule(60, BreakTimeDays.All), 600L), (subject.Rule, subject.UsedSeconds));
    }

    [Fact]
    public void Resolve_GroupedApp_OtherAccount_DefaultGroupRuleAndOwnTime()
    {
        var subject = LimitSubjectResolver.Resolve(_ledger, Kid2, Paint, "Paint", _apps, _groups, Today);

        Assert.Equal((LimitRule.Default, 0L), (subject.Rule, subject.UsedSeconds));
    }

    [Fact]
    public void Resolve_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => LimitSubjectResolver.Resolve(null!, Kid, Paint, "Paint", _apps, _groups, Today));
        Assert.Throws<ArgumentNullException>(() => LimitSubjectResolver.Resolve(_ledger, Kid, Paint, "Paint", null!, _groups, Today));
        Assert.Throws<ArgumentNullException>(() => LimitSubjectResolver.Resolve(_ledger, Kid, Paint, "Paint", _apps, null!, Today));
    }

    [Fact]
    public void WarningLedger_SubjectKey_AppKeepsItsFormat_GroupPerAccountAndDay()
    {
        var app = LimitSubjectResolver.Resolve(_ledger, Kid, Notepad, "Editor", _apps, _groups, Today);
        var group = LimitSubjectResolver.Resolve(_ledger, Kid, Paint, "Paint", _apps, _groups, Today);

        Assert.Equal(WarningLedger.AppKey(Kid, Notepad, Today), WarningLedger.SubjectKey(Kid, app, Today));
        Assert.Equal(new WarningKey($"group|{Kid.ToUpperInvariant()}|7", Today, null), WarningLedger.SubjectKey(Kid.ToLowerInvariant(), group, Today));
        Assert.Throws<ArgumentNullException>(() => WarningLedger.SubjectKey(null!, group, Today));
        Assert.Throws<ArgumentNullException>(() => WarningLedger.SubjectKey(Kid, null!, Today));
    }

    [Fact]
    public void EnforcementLog_GroupTexts()
    {
        Assert.Equal("Editor (notepad.exe)", EnforcementLog.Subject(null, [("Editor", "notepad.exe")]));
        Assert.Equal("group Games (Editor, Paint)", EnforcementLog.Subject("Games", [("Editor", "notepad.exe"), ("Paint", "mspaint.exe")]));
        Assert.Throws<ArgumentNullException>(() => EnforcementLog.Subject(null, null!));
        Assert.Equal("group rule 1:00, Sa, group used 1:00:02", EnforcementLog.RuleText(new AppRuleAtClose(60, BreakTimeDays.Saturday, 3602, 7, "Games")));
    }

    [Fact]
    public void EnforcementLog_BlockedGroupStartAndClose_NameTheGroup()
    {
        var logger = new TestLogger<EnforcementLog>();
        var log = new EnforcementLog(logger, new InstanceLogLimiter(new FakeTimeProvider(T0)));
        var start = EnforcementTestData.Start() with
        {
            Entry = null,
            Reason = CloseReasons.BudgetUsedUp,
            Rule = new AppRuleAtClose(60, BreakTimeDays.All, 3600, 7, "Games"),
        };

        log.Detected(start);
        log.Detected(start with { Kind = CloseKinds.RunningApp });
        log.Ended(start with { Kind = CloseKinds.RunningApp }, BlockedStartTexts.ClosedGracefully, "shown", detectionLogged: true);

        var messages = logger.Messages(LogLevel.Information);
        Assert.Contains(
            @"Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 5120, group Games, budget used up (group rule 1:00, Mo Tu We Th Fr Sa Su, group used 1:00:00); closing the app.",
            messages);
        Assert.Contains(@"Closing app: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), group Games, budget used up.", messages);
        Assert.Contains(@"App closed: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), group Games, budget used up: closed gracefully; message shown.", messages);
    }
}
