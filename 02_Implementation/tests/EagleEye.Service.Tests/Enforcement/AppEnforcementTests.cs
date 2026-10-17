using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class AppEnforcementTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<IBreakTimeService> _rules = new();
    private readonly Mock<IAppRuleService> _appRules = new();
    private readonly Mock<IUsageRepository> _usage = new();
    private readonly Mock<ICloseSequenceRunner> _runner = new();
    private readonly Mock<IBlockedStartRepository> _blocked = new();
    private readonly Mock<ITimeChangeFindingRepository> _findings = new();
    private readonly TestLogger<AppStartGate> _gateLogger = new();
    private readonly OpenAppsView _openApps = new();
    private readonly SessionAppsView _sessionApps = new();
    private readonly TodayUsageLedger _ledger;
    private readonly AppEnforcement _enforcement;

    public AppEnforcementTests()
    {
        _rules.SetupGet(r => r.Current).Returns(RulesSnapshot.Empty);
        _appRules.SetupGet(r => r.Current).Returns(AppRulesSnapshot.Empty.With(Kid, AccountAppRules.Empty.WithApp(new AppRuleEntry(1, Notepad, "Editor", 60, BreakTimeDays.All))));
        _usage.Setup(u => u.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var ignoreList = new EnforcementIgnoreList(@"C:\Windows");
        var registry = new CloseRegistry();
        var log = new EnforcementLog(new TestLogger<EnforcementLog>(), new InstanceLogLimiter(_time));
        var warnings = new Mock<IWarningSettingsService>();
        warnings.SetupGet(w => w.Current).Returns(WarningSettings.Defaults);
        _ledger = new TodayUsageLedger(_usage.Object, _appRules.Object, _time, new TestLogger<TodayUsageLedger>());
        var gate = new AppStartGate(_rules.Object, _appRules.Object, _ledger, Mock.Of<IProcessTable>(), _runner.Object, registry, ignoreList, _time, _gateLogger);
        var engine = new TimeLimitEngine(
            _rules.Object, _appRules.Object, warnings.Object, _ledger, _sessionApps, registry, _runner.Object, Mock.Of<IKidMessenger>(), ignoreList, log, _time);
        _enforcement = new AppEnforcement(
            gate,
            _ledger,
            engine,
            _sessionApps,
            new TimeChangeMonitor(_findings.Object, _time, new TestLogger<TimeChangeMonitor>()),
            _runner.Object,
            new EnforcementHistory(_blocked.Object, _findings.Object, _time, new TestLogger<EnforcementHistory>()),
            log,
            _openApps);
    }

    [Fact]
    public void Screen_DelegatesToTheGateAndRemembersTheSessionsApps()
    {
        var report = new AppsObserved(2, Kid, [new ObservedApp(Notepad, "notepad.exe", "Editor", [new ObservedProcess(10, 1)])], 1);

        Assert.Same(report, _enforcement.Screen(report, "kid1", _ => false));
        Assert.Same(report.Apps, _sessionApps.Of(2)!.Apps);
    }

    [Fact]
    public void PublishOpenApps_UpdatesTheView()
    {
        _enforcement.PublishOpenApps(new Dictionary<string, IReadOnlySet<string>> { ["S-1"] = new HashSet<string> { "a" } });

        Assert.Single(_openApps.Of("S-1"));
    }

    [Fact]
    public async Task OnTickAsync_LoadsTheLedgerAppliesCreditsAndRunsMonitorGateAndEngine()
    {
        var today = new DateOnly(2026, 10, 12);
        _usage.Setup(u => u.GetUsageAsync(Kid, today, today, It.IsAny<CancellationToken>())).ReturnsAsync([new DayAppUsage(today, 1, "Editor", 100)]);
        var controlled = new Dictionary<string, string> { [Kid] = "kid1" };

        await _enforcement.OnTickAsync(new EnforcementTick(controlled, [], [], (_, _) => false), CancellationToken.None);
        await _enforcement.OnTickAsync(new EnforcementTick(controlled, [], [new UsageCredit(Kid, Notepad, today, 5)], (_, _) => false), CancellationToken.None);

        Assert.Equal(105, _ledger.UsedSeconds(Kid, Notepad, today));
        _rules.VerifyGet(r => r.Current, Times.AtLeast(2));
    }

    [Fact]
    public async Task OnTickAsync_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _enforcement.OnTickAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task PurgeOldDataAsync_PurgesTheHistory()
    {
        await _enforcement.PurgeOldDataAsync(CancellationToken.None);

        _blocked.Verify(b => b.PurgeOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsTheRunner()
    {
        await _enforcement.StopAsync();

        _runner.Verify(r => r.StopAsync(), Times.Once);
    }

    [Fact]
    public void SessionEnded_ForgetsTheSessionsApps_PowerChangedDoesNotThrow()
    {
        _enforcement.Screen(new AppsObserved(2, Kid, [], 1), "kid1", _ => false);

        _enforcement.SessionEnded(2);
        _enforcement.PowerChanged();

        Assert.Null(_sessionApps.Of(2));
        Assert.Empty(_gateLogger.Entries);
    }
}
