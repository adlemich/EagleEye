using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>What the accounting loop needs of the break-time enforcement (US-005 Step 5.4).</summary>
public interface IBreakTimeEnforcement
{
    /// <summary>The report without blocked apps (<see cref="BreakTimeGate.Screen"/>).</summary>
    AppsObserved Screen(AppsObserved observed, string userName, Func<string, bool> isOpen);

    /// <summary>Publishes the open program paths per account for the kill sets.</summary>
    void PublishOpenApps(IReadOnlyDictionary<string, IReadOnlySet<string>> openPaths);

    /// <summary>The 5 s tick: refresh the time zone, find time changes, log break-time transitions and suppressed entries.</summary>
    Task OnTickAsync(IReadOnlyDictionary<string, string> controlledAccounts, IReadOnlyList<SessionInUse> sessionsInUse, CancellationToken ct);

    /// <summary>A session ended.</summary>
    void SessionEnded(int sessionId);

    /// <summary>The PC suspended or resumed.</summary>
    void PowerChanged();

    /// <summary>The first tick of a new local day: purge records older than 90 days.</summary>
    Task PurgeOldDataAsync(CancellationToken ct);

    /// <summary>Service stop: end the running close sequences.</summary>
    Task StopAsync();
}

/// <summary>Facade over gate, time-change monitor, runner, history and log, so the loop has one collaborator.</summary>
public sealed class BreakTimeEnforcement(
    BreakTimeGate gate,
    TimeChangeMonitor monitor,
    IBlockedStartRunner runner,
    EnforcementHistory history,
    BlockedStartLog log,
    OpenAppsView openApps) : IBreakTimeEnforcement
{
    /// <inheritdoc />
    public AppsObserved Screen(AppsObserved observed, string userName, Func<string, bool> isOpen) => gate.Screen(observed, userName, isOpen);

    /// <inheritdoc />
    public void PublishOpenApps(IReadOnlyDictionary<string, IReadOnlySet<string>> openPaths) => openApps.Publish(openPaths);

    /// <inheritdoc />
    public async Task OnTickAsync(IReadOnlyDictionary<string, string> controlledAccounts, IReadOnlyList<SessionInUse> sessionsInUse, CancellationToken ct)
    {
        // .NET caches the local zone for the process lifetime; a zone change must apply within 5 s (ADR-012 amendment).
        TimeZoneInfo.ClearCachedData();
        await monitor.OnTickAsync(sessionsInUse, ct).ConfigureAwait(false);
        gate.OnTick(controlledAccounts);
        log.Flush();
    }

    /// <inheritdoc />
    public void SessionEnded(int sessionId) => gate.RemoveSession(sessionId);

    /// <inheritdoc />
    public void PowerChanged() => monitor.Rebaseline();

    /// <inheritdoc />
    public Task PurgeOldDataAsync(CancellationToken ct) => history.PurgeOldDataAsync(ct);

    /// <inheritdoc />
    public Task StopAsync() => runner.StopAsync();
}
