using System.Collections.Concurrent;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Monitoring;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageAccountingLoopTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Notepad = @"C:\Windows\notepad.exe";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));
    private readonly UsageEventQueue _queue = new();
    private readonly Mock<ISessionSource> _sessions = new();
    private readonly Mock<ISessionAgentSupervisor> _supervisor = new();
    private readonly Mock<IUsageService> _usage = new();
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly Mock<IBreakTimeEnforcement> _enforcement = new();
    private readonly TestLogger<UsageAccountingLoop> _logger = new();
    private readonly ConcurrentQueue<(TrackerOutput Output, bool IsTick)> _applied = new();
    private List<SessionInfo> _currentSessions = [new(2, Kid, SessionConnectState.Active, false)];
    private List<ControlledAccount> _controlled = [new(Kid, "kid1")];
    private UsageAccountingLoop _loop = null!;
    private bool _stopped;

    public async Task InitializeAsync()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _enforcement.Setup(e => e.Screen(It.IsAny<AppsObserved>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>()))
            .Returns<AppsObserved, string, Func<string, bool>>((observed, _, _) => observed);
        _enforcement.Setup(e => e.OnTickAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<IReadOnlyList<SessionInUse>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _enforcement.Setup(e => e.PurgeOldDataAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _enforcement.Setup(e => e.StopAsync()).Returns(Task.CompletedTask);
        _sessions.Setup(s => s.GetSessions()).Returns(() => _currentSessions);
        _supervisor.Setup(s => s.IsObserving(It.IsAny<int>())).Returns(true);
        _accounts.Setup(a => a.GetControlledAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _controlled);
        _usage.Setup(u => u.ApplyAsync(It.IsAny<TrackerOutput>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<TrackerOutput, IReadOnlyDictionary<string, string>, bool, CancellationToken>((o, _, tick, _) => _applied.Enqueue((o, tick)))
            .Returns(Task.CompletedTask);
        _loop = new UsageAccountingLoop(
            _queue, _sessions.Object, _supervisor.Object, new UsageTracker(_time, new TestLogger<UsageTracker>()),
            _usage.Object, _accounts.Object, _enforcement.Object, _time, _logger);
        await _loop.StartAsync(CancellationToken.None);
        await WaitFor(() => Ticks() == 1);
    }

    public async Task DisposeAsync()
    {
        if (!_stopped)
        {
            await _loop.StopAsync(CancellationToken.None);
        }

        _loop.Dispose();
    }

    [Fact]
    public void Start_ImmediateTickReconcilesSessionsAccountsAndAgents()
    {
        _supervisor.Verify(s => s.ReconcileAsync(
            It.Is<IReadOnlyCollection<SessionInfo>>(c => c.Single().SessionId == 2),
            It.Is<IReadOnlyDictionary<string, string>>(d => d[Kid] == "kid1")), Times.Once);
    }

    [Fact]
    public async Task Tick_Every5Seconds()
    {
        _time.Advance(UsageAccountingLoop.TickInterval);

        await WaitFor(() => Ticks() == 2);
    }

    [Fact]
    public async Task AppsObserved_ControlledAccount_StartsInstanceAtOnce()
    {
        Observe();

        await WaitFor(() => Started().Any());
        Assert.Contains(_applied, a => !a.IsTick && a.Output.Instances.Count == 1);
    }

    [Fact]
    public async Task AppsObserved_UncontrolledAccount_Ignored()
    {
        _queue.Enqueue(new AppsObserved(3, "S-1-5-21-9", [App()]));
        await TickAsync();

        Assert.Empty(Started());
    }

    [Fact]
    public async Task ActiveSession_CreditedAtTick()
    {
        Observe();
        await WaitFor(() => Started().Any());

        await TickAsync();

        Assert.Equal(5, Credited());
    }

    [Fact]
    public async Task NotObservedByAgent_NotCredited()
    {
        _supervisor.Setup(s => s.IsObserving(2)).Returns(false);
        Observe();
        await WaitFor(() => Started().Any());

        await TickAsync();

        Assert.Equal(0, Credited());
    }

    [Fact]
    public async Task LockEvent_StopsCreditingAtOnce()
    {
        Observe();
        await WaitFor(() => Started().Any());
        _queue.Enqueue(new SessionChanged(2, SessionChangeKind.Lock));
        await WaitFor(() => _applied.Count(a => !a.IsTick) == 2);
        _currentSessions = [new(2, Kid, SessionConnectState.Active, true)];

        await TickAsync();

        Assert.Equal(0, Credited());
    }

    [Fact]
    public async Task LogoffEvent_EndsInstancesWithSessionEnded()
    {
        Observe();
        await WaitFor(() => Started().Any());

        _queue.Enqueue(new SessionChanged(2, SessionChangeKind.Logoff));

        await WaitFor(() => Ended().Any(e => e.Reason == EndReasons.SessionEnded));
        _enforcement.Verify(e => e.SessionEnded(2), Times.Once);
    }

    [Fact]
    public async Task SessionGoneFromSnapshot_EndsInstances()
    {
        Observe();
        await WaitFor(() => Started().Any());
        _currentSessions = [];

        await TickAsync();

        Assert.Contains(Ended(), e => e.Reason == EndReasons.SessionEnded);
        _enforcement.Verify(e => e.SessionEnded(2), Times.Once);
    }

    [Fact]
    public async Task Untick_StopsRecordingWithLog()
    {
        Observe();
        await WaitFor(() => Started().Any());
        _controlled = [];

        await TickAsync();

        Assert.Contains(Ended(), e => e.Reason == EndReasons.MonitoringStopped);
        Assert.Contains($"Usage recording stopped for account kid1 ({Kid}): monitoring stopped.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task SuspendAndResume_SleepNotCredited()
    {
        Observe();
        await WaitFor(() => Started().Any());
        _queue.Enqueue(new PowerSuspended());
        await WaitFor(() => _applied.Count(a => !a.IsTick) == 2);

        await TickAsync();
        _queue.Enqueue(new PowerResumed());
        await WaitFor(() => _applied.Count(a => !a.IsTick) == 3);

        Assert.Equal(0, Credited());
    }

    [Fact]
    public async Task SuspendAndResume_EnforcementRebaselines()
    {
        _queue.Enqueue(new PowerSuspended());
        _queue.Enqueue(new PowerResumed());

        await WaitFor(() => _enforcement.Invocations.Count(i => i.Method.Name == nameof(IBreakTimeEnforcement.PowerChanged)) == 2);
    }

    [Fact]
    public async Task AppsObserved_ScreenedBeforeTheTracker_BlockedAppsNeverStart()
    {
        _enforcement.Setup(e => e.Screen(It.IsAny<AppsObserved>(), "kid1", It.IsAny<Func<string, bool>>()))
            .Returns<AppsObserved, string, Func<string, bool>>((observed, _, isOpen) => isOpen(Notepad) ? observed : observed with { Apps = [] });

        Observe();
        await TickAsync();

        Assert.Empty(Started());
        _enforcement.Verify(e => e.PublishOpenApps(It.IsAny<IReadOnlyDictionary<string, IReadOnlySet<string>>>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Tick_EnforcementGetsControlledAccountsAndSessionsInUse()
    {
        _currentSessions = [new(2, Kid, SessionConnectState.Active, false), new(3, "S-1-5-21-9", SessionConnectState.Active, false), new(4, Kid, SessionConnectState.Active, true)];

        await TickAsync();

        _enforcement.Verify(e => e.OnTickAsync(
            It.Is<IReadOnlyDictionary<string, string>>(d => d[Kid] == "kid1"),
            It.Is<IReadOnlyList<SessionInUse>>(s => s.Count == 1 && s[0] == new SessionInUse(2, Kid, "kid1")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Stop_StopsTheEnforcement()
    {
        await StopAsync();

        _enforcement.Verify(e => e.StopAsync(), Times.Once);
    }

    [Fact]
    public async Task Midnight_PurgesAndPublishesTodayOnce()
    {
        _time.Advance(TimeSpan.FromHours(12));
        await WaitFor(() => _usage.Invocations.Any(i => i.Method.Name == nameof(IUsageService.PublishTodayAsync)));
        await TickAsync();

        _usage.Verify(u => u.PurgeOldDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        _enforcement.Verify(e => e.PurgeOldDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        _usage.Verify(u => u.PublishTodayAsync(It.Is<IReadOnlyCollection<string>>(c => c.Single() == Kid), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailingIteration_LoggedAndLoopContinues()
    {
        var failure = new InvalidOperationException("boom");
        _supervisor.Setup(s => s.ReconcileAsync(It.IsAny<IReadOnlyCollection<SessionInfo>>(), It.IsAny<IReadOnlyDictionary<string, string>>())).ThrowsAsync(failure);
        _time.Advance(UsageAccountingLoop.TickInterval);
        await WaitFor(() => _logger.Has(LogLevel.Error, failure));

        Observe();

        await WaitFor(() => Started().Any());
    }

    [Fact]
    public async Task SessionSourceFails_WarningAndLastStateKept()
    {
        var failure = new System.ComponentModel.Win32Exception(5);
        _sessions.Setup(s => s.GetSessions()).Throws(failure);
        Observe();
        await WaitFor(() => Started().Any());

        await TickAsync();

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Equal(5, Credited());
    }

    [Fact]
    public async Task Stop_EndsInstancesPersistsStopsAgentsAndCompletesQueue()
    {
        Observe();
        await WaitFor(() => Started().Any());

        await StopAsync();

        Assert.Contains(_applied, a => a.IsTick && a.Output.Instances.OfType<InstanceEnded>().Any(e => e.Reason == EndReasons.ServiceStopping));
        _supervisor.Verify(s => s.StopAllAsync(), Times.Once);
        Assert.False(_queue.Enqueue(new UsageTick()));
    }

    [Fact]
    public async Task Stop_PersistFails_ErrorAndAgentsStillStopped()
    {
        var failure = new IOException("disk");
        _usage.Setup(u => u.ApplyAsync(It.IsAny<TrackerOutput>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        await StopAsync();

        Assert.True(_logger.Has(LogLevel.Error, failure));
        _supervisor.Verify(s => s.StopAllAsync(), Times.Once);
    }

    private static ObservedApp App() => new(Notepad, "notepad.exe", "Editor", [new ObservedProcess(10, 1)]);

    private void Observe() => _queue.Enqueue(new AppsObserved(2, Kid, [App()]));

    private int Ticks() => _applied.Count(a => a.IsTick);

    private IEnumerable<InstanceStarted> Started() => _applied.SelectMany(a => a.Output.Instances).OfType<InstanceStarted>();

    private IEnumerable<InstanceEnded> Ended() => _applied.SelectMany(a => a.Output.Instances).OfType<InstanceEnded>();

    private long Credited() => _applied.SelectMany(a => a.Output.Credits).Sum(c => c.Seconds);

    private async Task TickAsync()
    {
        var ticks = Ticks();
        _time.Advance(UsageAccountingLoop.TickInterval);
        await WaitFor(() => Ticks() > ticks);
    }

    private async Task StopAsync()
    {
        _stopped = true;
        await _loop.StopAsync(CancellationToken.None);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(condition());
    }
}
