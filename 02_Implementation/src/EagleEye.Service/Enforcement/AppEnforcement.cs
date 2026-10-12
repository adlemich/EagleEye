using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>What one accounting tick hands to the enforcement (US-006 Decision 5).</summary>
/// <param name="ControlledAccounts">User names by SID of the controlled accounts.</param>
/// <param name="SessionsInUse">The controlled accounts' sessions in use (time-change findings).</param>
/// <param name="Credits">The credits of this tick, exactly as persisted by the usage service.</param>
/// <param name="IsOpen">Whether a program path is open for an account in the tracker.</param>
public sealed record EnforcementTick(
    IReadOnlyDictionary<string, string> ControlledAccounts,
    IReadOnlyList<SessionInUse> SessionsInUse,
    IReadOnlyList<UsageCredit> Credits,
    Func<string, string, bool> IsOpen);

/// <summary>What the accounting loop needs of the enforcement (US-005 Step 5.4, US-006 Decision 5).</summary>
public interface IAppEnforcement
{
    /// <summary>The report without blocked starts (<see cref="AppStartGate.Screen"/>); remembers the screened apps of the session.</summary>
    AppsObserved Screen(AppsObserved observed, string userName, Func<string, bool> isOpen);

    /// <summary>Publishes the open program paths per account for the kill sets.</summary>
    void PublishOpenApps(IReadOnlyDictionary<string, IReadOnlySet<string>> openPaths);

    /// <summary>
    /// The 5 s tick, after the usage of the tick is persisted: feed the budget ledger, refresh the time zone, find time
    /// changes, log break-time transitions, evaluate the time limits of running apps, flush suppressed log entries.
    /// </summary>
    Task OnTickAsync(EnforcementTick tick, CancellationToken ct);

    /// <summary>A session ended.</summary>
    void SessionEnded(int sessionId);

    /// <summary>The PC suspended or resumed.</summary>
    void PowerChanged();

    /// <summary>The first tick of a new local day: purge records older than 90 days.</summary>
    Task PurgeOldDataAsync(CancellationToken ct);

    /// <summary>Service stop: end the running close sequences.</summary>
    Task StopAsync();
}

/// <summary>Facade over gate, ledger, engine, time-change monitor, runner, history and log, so the loop has one collaborator.</summary>
public sealed class AppEnforcement(
    AppStartGate gate,
    TodayUsageLedger ledger,
    TimeLimitEngine engine,
    SessionAppsView sessionApps,
    TimeChangeMonitor monitor,
    ICloseSequenceRunner runner,
    EnforcementHistory history,
    EnforcementLog log,
    OpenAppsView openApps) : IAppEnforcement
{
    /// <inheritdoc />
    public AppsObserved Screen(AppsObserved observed, string userName, Func<string, bool> isOpen)
    {
        var screened = gate.Screen(observed, userName, isOpen);
        sessionApps.Update(screened.SessionId, screened.AccountSid, screened.Apps);
        return screened;
    }

    /// <inheritdoc />
    public void PublishOpenApps(IReadOnlyDictionary<string, IReadOnlySet<string>> openPaths) => openApps.Publish(openPaths);

    /// <inheritdoc />
    public async Task OnTickAsync(EnforcementTick tick, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tick);

        // .NET caches the local zone for the process lifetime; a zone change must apply within 5 s (ADR-012 amendment).
        TimeZoneInfo.ClearCachedData();
        ledger.Apply(tick.Credits);
        await ledger.EnsureLoadedAsync([.. tick.ControlledAccounts.Keys], ct).ConfigureAwait(false);
        await monitor.OnTickAsync(tick.SessionsInUse, ct).ConfigureAwait(false);
        gate.OnTick(tick.ControlledAccounts);
        engine.Evaluate(tick.ControlledAccounts, tick.IsOpen);
        log.Flush();
    }

    /// <inheritdoc />
    public void SessionEnded(int sessionId)
    {
        gate.RemoveSession(sessionId);
        sessionApps.Remove(sessionId);
    }

    /// <inheritdoc />
    public void PowerChanged() => monitor.Rebaseline();

    /// <inheritdoc />
    public Task PurgeOldDataAsync(CancellationToken ct) => history.PurgeOldDataAsync(ct);

    /// <inheritdoc />
    public Task StopAsync() => runner.StopAsync();
}
