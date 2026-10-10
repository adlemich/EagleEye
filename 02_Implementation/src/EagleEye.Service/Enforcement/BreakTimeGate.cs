using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Decides blocked starts (ADR-013 §1, §3; US-005 Decision 3). Called by the accounting loop for every report of a
/// controlled account <b>before</b> the usage tracker; never awaits anything. Start = creation of the app's first
/// process (Q-1):
/// <list type="bullet">
/// <item>an app that is open for the account is never a start; its new processes pass to the tracker;</item>
/// <item>an app that is being closed: its newly reported processes are removed and added to the running sequence;</item>
/// <item>an app that becomes open: the earliest creation time of its processes (from the report; the process table is
/// read only if that time lies in a break, to find an older process of the same path) decides — blocked iff an entry
/// was in effect at that local time.</item>
/// </list>
/// Blocked apps are removed from the report until their sequence has ended, so they create no usage (AC-24). Accounts
/// without an active entry pass unchanged (AC-29). Also logs when break times begin and end. Used by the loop only.
/// </summary>
public sealed class BreakTimeGate(
    IBreakTimeService rules,
    IProcessTable processTable,
    IBlockedStartRunner runner,
    EnforcementIgnoreList ignoreList,
    TimeProvider timeProvider,
    ILogger<BreakTimeGate> logger)
{
    private readonly Dictionary<int, SessionState> _sessions = [];
    private readonly Dictionary<string, BreakTimeEntryDto> _inEffect = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the report without the processes of blocked apps; starts close sequences for new blocked starts.</summary>
    /// <param name="observed">The report of a controlled account.</param>
    /// <param name="userName">The account's user name.</param>
    /// <param name="isOpen">Whether a program path is open for the account (the tracker, incl. its merge window).</param>
    public AppsObserved Screen(AppsObserved observed, string userName, Func<string, bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(isOpen);
        var state = StateOf(observed.SessionId, observed.AccountSid);
        var firstOfRun = state.Run != observed.AgentRun;
        state.Run = observed.AgentRun;
        foreach (var path in state.Blocked.Where(b => b.Value.Handle.IsCompleted).Select(b => b.Key).ToList())
        {
            state.Blocked.Remove(path);
        }

        var accountRules = rules.Current.For(observed.AccountSid);
        if (!accountRules.HasActiveEntry && state.Blocked.Count == 0)
        {
            return observed;
        }

        var kept = new List<ObservedApp>(observed.Apps.Count);
        foreach (var app in observed.Apps)
        {
            if (state.Blocked.TryGetValue(app.ProgramPath, out var blocked))
            {
                var fresh = app.Processes.Where(p => blocked.Known.Add((p.Pid, p.Created))).ToList();
                if (fresh.Count > 0)
                {
                    blocked.Handle.AddProcesses(fresh);
                }
            }
            else if (ignoreList.IsIgnored(app.ProgramPath) || isOpen(app.ProgramPath) || !accountRules.HasActiveEntry
                || Decide(observed, app, accountRules, userName, firstOfRun) is not { } start)
            {
                kept.Add(app);
            }
            else
            {
                state.Blocked[app.ProgramPath] = new BlockedApp(runner.Start(start), [.. app.Processes.Select(p => (p.Pid, p.Created))]);
            }
        }

        return kept.Count == observed.Apps.Count ? observed : observed with { Apps = kept };
    }

    /// <summary>Every 5 s: logs when a break time of a controlled account begins or ends.</summary>
    public void OnTick(IReadOnlyDictionary<string, string> controlledAccounts)
    {
        ArgumentNullException.ThrowIfNull(controlledAccounts);
        var local = BreakTimeSchedule.ToLocal(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone);
        foreach (var sid in _inEffect.Keys.ToList())
        {
            if (!controlledAccounts.ContainsKey(sid))
            {
                _inEffect.Remove(sid);
            }
        }

        foreach (var (sid, userName) in controlledAccounts)
        {
            var now = BreakTimeSchedule.FirstInEffect(rules.Current.For(sid).Entries, local);
            _inEffect.TryGetValue(sid, out var before);
            if (before is not null && before.EntryId != now?.EntryId)
            {
                logger.LogInformation("Break time of account {UserName} ended: {Range} ({Days}).", userName, BreakTimeChangeLog.Range(before), BreakTimeChangeLog.Days(before.Days));
                _inEffect.Remove(sid);
            }

            if (now is not null && before?.EntryId != now.EntryId)
            {
                logger.LogInformation("Break time of account {UserName} began: {Range} ({Days}).", userName, BreakTimeChangeLog.Range(now), BreakTimeChangeLog.Days(now.Days));
                _inEffect[sid] = now;
            }
        }
    }

    /// <summary>The session ended: forget its state (running sequences go on).</summary>
    public void RemoveSession(int sessionId) => _sessions.Remove(sessionId);

    private BlockedStart? Decide(AppsObserved observed, ObservedApp app, AccountRules accountRules, string userName, bool firstOfRun)
    {
        if (app.Processes.Count == 0)
        {
            return null;
        }

        var zone = timeProvider.LocalTimeZone;
        var created = app.Processes.Min(p => p.Created);
        if (InEffectAt(accountRules, created, zone) is null)
        {
            return null;
        }

        created = Math.Min(created, OldestProcess(observed, app.ProgramPath, created));
        if (InEffectAt(accountRules, created, zone) is not { } entry)
        {
            return null;
        }

        var startedUtc = DateTimeOffset.FromFileTime(created).ToUniversalTime();
        return new BlockedStart(
            observed.SessionId, observed.AccountSid, userName, app.ProgramPath, app.ProcessName, app.DisplayName, app.Processes,
            firstOfRun ? BlockedStartTexts.AgentStartTrigger : BlockedStartTexts.AppStartTrigger, entry, accountRules.DisplayText,
            startedUtc, BreakTimeSchedule.ToLocal(startedUtc, zone), timeProvider.GetUtcNow());
    }

    /// <summary>The creation time of the oldest process of the path in the session, owned by the account.</summary>
    private long OldestProcess(AppsObserved observed, string programPath, long fallback)
    {
        try
        {
            return processTable.Snapshot(observed.SessionId)
                .Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.ImagePath, programPath)
                    && StringComparer.OrdinalIgnoreCase.Equals(r.OwnerSid, observed.AccountSid))
                .Select(r => r.Created)
                .DefaultIfEmpty(fallback)
                .Min();
        }
        catch (Exception ex)
        {
            // Win32 boundary: without the table the reported processes decide.
            logger.LogWarning(ex, "Reading the processes of session {SessionId} failed; the reported processes decide.", observed.SessionId);
            return fallback;
        }
    }

    private static BreakTimeEntryDto? InEffectAt(AccountRules accountRules, long created, TimeZoneInfo zone) =>
        BreakTimeSchedule.FirstInEffect(accountRules.Entries, BreakTimeSchedule.ToLocal(DateTimeOffset.FromFileTime(created), zone));

    private SessionState StateOf(int sessionId, string accountSid)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || !StringComparer.OrdinalIgnoreCase.Equals(state.AccountSid, accountSid))
        {
            state = new SessionState(accountSid);
            _sessions[sessionId] = state;
        }

        return state;
    }

    private sealed class SessionState(string accountSid)
    {
        public string AccountSid { get; } = accountSid;

        public long? Run { get; set; }

        public Dictionary<string, BlockedApp> Blocked { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record BlockedApp(IBlockedStartHandle Handle, HashSet<(int Pid, long Created)> Known);
}
