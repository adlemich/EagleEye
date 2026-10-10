using System.Threading.Channels;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Monitoring;
using EagleEye.Service.UserAccounts;

namespace EagleEye.Service.Statistics;

/// <summary>
/// The single consumer of <see cref="UsageEventQueue"/> (ADR-012 §4): a tick every 5 s reconciles the sessions (WTS),
/// the controlled accounts (US-003) and the session agents, credits usage, persists it and broadcasts changed days;
/// agent reports and session/power notifications are applied at once. A failing iteration is logged and the loop
/// goes on (coding guidelines §5.3). Stopping ends all instances with "service stopping", persists and stops the agents.
/// </summary>
public sealed class UsageAccountingLoop(
    UsageEventQueue queue,
    ISessionSource sessionSource,
    ISessionAgentSupervisor supervisor,
    UsageTracker tracker,
    IUsageService usage,
    IUserAccountService userAccounts,
    IBreakTimeEnforcement enforcement,
    TimeProvider timeProvider,
    ILogger<UsageAccountingLoop> logger) : BackgroundService
{
    /// <summary>Accounting interval (AC-13).</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly SessionStateTracker _sessions = new();
    private Dictionary<string, string> _controlled = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly _today;

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            tracker.StopAll(EndReasons.ServiceStopping);
            await usage.ApplyAsync(tracker.TakeOutput(includeUsage: true), _controlled, isTick: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Shutdown boundary: the agents must still be stopped; open instances are closed at the next start.
            logger.LogError(ex, "Ending the open app instances at service stop failed.");
        }

        // US-005 D-14: running close sequences terminate their apps at once and record it.
        await enforcement.StopAsync().ConfigureAwait(false);
        await supervisor.StopAllAsync().ConfigureAwait(false);
        queue.Complete();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _today = Today();
        queue.Enqueue(new UsageTick());
        var ticker = TickerAsync(stoppingToken);
        while (await NextAsync(stoppingToken).ConfigureAwait(false) is { } usageEvent)
        {
            try
            {
                await HandleAsync(usageEvent, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Background loop boundary: one failed event must not end usage accounting.
                logger.LogError(ex, "Usage accounting failed for {Event}.", usageEvent.GetType().Name);
            }
        }

        await ticker.ConfigureAwait(false);
    }

    private async Task<UsageEvent?> NextAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ChannelClosedException)
        {
            return null;
        }
    }

    private async Task TickerAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval, timeProvider);
        try
        {
            while (true)
            {
                // The result is always true: the local timer is only disposed after the loop has ended.
                _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                queue.Enqueue(new UsageTick());
            }
        }
        catch (OperationCanceledException)
        {
            // The service stops.
        }
    }

    private async Task HandleAsync(UsageEvent usageEvent, CancellationToken ct)
    {
        var isTick = usageEvent is UsageTick;
        switch (usageEvent)
        {
            case UsageTick:
                await TickAsync().ConfigureAwait(false);
                await enforcement.OnTickAsync(_controlled, SessionsInUse(), ct).ConfigureAwait(false);
                break;
            case AppsObserved observed when _controlled.TryGetValue(observed.AccountSid, out var userName):
                // US-005 (ADR-013 §1): blocked starts are removed before the tracker, so they never count as usage.
                var screened = enforcement.Screen(observed, userName, path => tracker.IsOpen(observed.AccountSid, path));
                tracker.ObserveApps(screened.SessionId, screened.AccountSid, screened.Apps);
                break;
            case SessionChanged changed:
                tracker.Advance();
                _sessions.ApplyChange(changed.SessionId, changed.Kind);
                if (changed.Kind == SessionChangeKind.Logoff)
                {
                    tracker.RemoveSession(changed.SessionId);
                    enforcement.SessionEnded(changed.SessionId);
                }

                break;
            case PowerSuspended:
                tracker.Suspend();
                enforcement.PowerChanged();
                break;
            case PowerResumed:
                tracker.Resume();
                enforcement.PowerChanged();
                break;
        }

        tracker.SetActivity(sid => _sessions.AccountInUse(sid, supervisor.IsObserving));
        await usage.ApplyAsync(tracker.TakeOutput(isTick), _controlled, isTick, ct).ConfigureAwait(false);
        enforcement.PublishOpenApps(tracker.OpenPaths());
        if (isTick && Today() is var today && today != _today)
        {
            // First tick of a new local day (ADR-012 §5, §6): purge, then the new (empty) today for every account.
            _today = today;
            await usage.PurgeOldDataAsync(ct).ConfigureAwait(false);
            await enforcement.PurgeOldDataAsync(ct).ConfigureAwait(false);
            await usage.PublishTodayAsync([.. _controlled.Keys], ct).ConfigureAwait(false);
        }
    }

    /// <summary>The controlled accounts' sessions that are in use now (for time-change findings, AC-37).</summary>
    private List<SessionInUse> SessionsInUse() =>
        [.. _sessions.Sessions
            .Where(s => s.IsInUse && s.UserSid is not null && _controlled.ContainsKey(s.UserSid))
            .Select(s => new SessionInUse(s.SessionId, s.UserSid!, _controlled[s.UserSid!]))]; // Checked for null above.

    private async Task TickAsync()
    {
        tracker.Advance();
        ReconcileSessions();
        var controlled = (await userAccounts.GetControlledAccountsAsync().ConfigureAwait(false))
            .ToDictionary(a => a.Sid, a => a.UserName, StringComparer.OrdinalIgnoreCase);
        foreach (var (sid, userName) in _controlled.Where(c => !controlled.ContainsKey(c.Key)))
        {
            tracker.StopAccount(sid, EndReasons.MonitoringStopped);
            logger.LogInformation("Usage recording stopped for account {UserName} ({AccountSid}): {Reason}.", userName, sid, EndReasons.MonitoringStopped);
        }

        _controlled = controlled;
        await supervisor.ReconcileAsync(_sessions.Sessions, controlled).ConfigureAwait(false);
    }

    private void ReconcileSessions()
    {
        IReadOnlyList<SessionInfo> snapshot;
        try
        {
            snapshot = sessionSource.GetSessions();
        }
        catch (Exception ex)
        {
            // Win32 boundary: keep the last known sessions; the next tick tries again.
            logger.LogWarning(ex, "Reading the sessions failed; the last known state is kept.");
            return;
        }

        foreach (var known in _sessions.Sessions.ToList())
        {
            if (!snapshot.Any(s => s.SessionId == known.SessionId && StringComparer.OrdinalIgnoreCase.Equals(s.UserSid, known.UserSid)))
            {
                tracker.RemoveSession(known.SessionId);
                enforcement.SessionEnded(known.SessionId);
            }
        }

        _sessions.ApplySnapshot(snapshot);
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone).DateTime);
}
