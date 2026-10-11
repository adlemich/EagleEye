using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class BreakTimeEnforcementTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IBreakTimeService> _rules = new();
    private readonly Mock<IBlockedStartRunner> _runner = new();
    private readonly Mock<IBlockedStartRepository> _blocked = new();
    private readonly Mock<ITimeChangeFindingRepository> _findings = new();
    private readonly TestLogger<BreakTimeGate> _gateLogger = new();
    private readonly OpenAppsView _openApps = new();
    private readonly BreakTimeEnforcement _enforcement;

    public BreakTimeEnforcementTests()
    {
        _rules.SetupGet(r => r.Current).Returns(RulesSnapshot.Empty);
        var gate = new BreakTimeGate(_rules.Object, Mock.Of<IProcessTable>(), _runner.Object, new EnforcementIgnoreList(@"C:\Windows"), _time, _gateLogger);
        _enforcement = new BreakTimeEnforcement(
            gate,
            new TimeChangeMonitor(_findings.Object, _time, new TestLogger<TimeChangeMonitor>()),
            _runner.Object,
            new EnforcementHistory(_blocked.Object, _findings.Object, _time, new TestLogger<EnforcementHistory>()),
            new BlockedStartLog(new TestLogger<BlockedStartLog>(), new InstanceLogLimiter(_time)),
            _openApps);
    }

    [Fact]
    public void Screen_DelegatesToTheGate()
    {
        var report = new AppsObserved(2, "S-1", [], 1);

        Assert.Same(report, _enforcement.Screen(report, "kid1", _ => false));
    }

    [Fact]
    public void PublishOpenApps_UpdatesTheView()
    {
        _enforcement.PublishOpenApps(new Dictionary<string, IReadOnlySet<string>> { ["S-1"] = new HashSet<string> { "a" } });

        Assert.Single(_openApps.Of("S-1"));
    }

    [Fact]
    public async Task OnTickAsync_RunsMonitorAndGate()
    {
        await _enforcement.OnTickAsync(new Dictionary<string, string> { ["S-1"] = "kid1" }, [], CancellationToken.None);

        _rules.VerifyGet(r => r.Current, Times.Once);
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
    public void SessionEndedAndPowerChanged_DoNotThrow()
    {
        _enforcement.SessionEnded(2);
        _enforcement.PowerChanged();

        Assert.Empty(_gateLogger.Entries);
    }
}
