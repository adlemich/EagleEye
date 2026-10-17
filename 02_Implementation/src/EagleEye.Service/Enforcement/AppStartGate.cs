using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Decides blocked starts (ADR-013 §1, §3; ADR-015 §6; US-005 Decision 3; US-006 Decision 2). Called by the accounting
/// loop for every report of a controlled account <b>before</b> the usage tracker; never awaits anything. Start = creation
/// of the app's first process (US-005 Q-1):
/// <list type="bullet">
/// <item>an app that is open for the account is never a start; its new processes pass to the tracker;</item>
/// <item>an app that is being closed (<see cref="CloseRegistry"/>): its newly reported processes are added to the running
/// sequence; they are removed from the report for blocked starts only (running apps keep counting usage, TI-8);</item>
/// <item>an app that becomes open is checked in this order: a break entry in effect at the start's creation time (the
/// process table is read only if the earliest reported creation time lies in a break, to find an older process of the
/// same path) → blocked with the break display text; otherwise the app rule at the local time of detection (no rule =
/// 0:00, no day): not allowed today, or used ≥ budget → blocked with the budget text.</item>
/// </list>
/// Blocked apps are removed from the report until their sequence has ended, so they create no usage (US-005 AC-24,
/// US-006 AC-19). Also logs when break times begin and end. Used by the loop only.
/// </summary>
public sealed class AppStartGate(
    IBreakTimeService rules,
    IAppRuleService appRules,
    TodayUsageLedger ledger,
    IProcessTable processTable,
    ICloseSequenceRunner runner,
    CloseRegistry registry,
    EnforcementIgnoreList ignoreList,
    TimeProvider timeProvider,
    ILogger<AppStartGate> logger)
{
    private const int SecondsPerMinute = 60;

    private readonly Dictionary<int, SessionState> _sessions = [];
    private readonly Dictionary<string, BreakTimeEntryDto> _inEffect = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the report without the processes of blocked starts; starts close sequences for new blocked starts.</summary>
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
        var context = new ScreenContext(observed, userName, firstOfRun, rules.Current.For(observed.AccountSid), appRules.Current.For(observed.AccountSid));

        var kept = new List<ObservedApp>(observed.Apps.Count);
        foreach (var app in observed.Apps)
        {
            if (registry.Find(observed.SessionId, app.ProgramPath) is { } closing)
            {
                var fresh = app.Processes.Where(p => closing.Known.Add((p.Pid, p.Created))).ToList();
                if (fresh.Count > 0)
                {
                    closing.Handle.AddProcesses(fresh);
                }

                if (!closing.RemovesFromReport)
                {
                    kept.Add(app);
                }
            }
            else if (ignoreList.IsIgnored(app.ProgramPath) || isOpen(app.ProgramPath) || Decide(context, app) is not { } request)
            {
                kept.Add(app);
            }
            else
            {
                registry.Add(observed.SessionId, app.ProgramPath, new CloseEntry(runner.Start(request), true, [.. app.Processes.Select(p => (p.Pid, p.Created))]));
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
    public void RemoveSession(int sessionId)
    {
        _sessions.Remove(sessionId);
        registry.RemoveSession(sessionId);
    }

    private CloseRequest? Decide(ScreenContext context, ObservedApp app)
    {
        if (app.Processes.Count == 0)
        {
            return null;
        }

        var zone = timeProvider.LocalTimeZone;
        var created = app.Processes.Min(p => p.Created);
        if (context.Breaks.HasActiveEntry && InEffectAt(context.Breaks, created, zone) is not null)
        {
            created = Math.Min(created, OldestProcess(context.Observed, app.ProgramPath, created));
            if (InEffectAt(context.Breaks, created, zone) is { } entry)
            {
                return NewRequest(context, app, created, zone, entry) with { Message = KidMessageFactory.BreakTime(context.Breaks.CustomText, entry) };
            }
        }

        return DecideByRule(context, app, created, zone);
    }

    /// <summary>US-006 Decision 2, steps 3 and 4: the app rule at the local time of detection.</summary>
    private CloseRequest? DecideByRule(ScreenContext context, ObservedApp app, long created, TimeZoneInfo zone)
    {
        var now = BreakTimeSchedule.ToLocal(timeProvider.GetUtcNow(), zone);
        var rule = context.Apps.ByPath.GetValueOrDefault(app.ProgramPath);
        var budget = rule?.BudgetMinutes ?? AppRuleRules.DefaultBudgetMinutes;
        var days = rule?.Days ?? AppRuleRules.DefaultDays;
        var used = ledger.UsedSeconds(context.Observed.AccountSid, app.ProgramPath, DateOnly.FromDateTime(now));
        string reason;
        if (!AppRuleRules.IsAllowedOn(budget, days, now.DayOfWeek))
        {
            reason = CloseReasons.NotAllowedToday;
        }
        else if (used >= (long)budget * SecondsPerMinute)
        {
            reason = CloseReasons.BudgetUsedUp;
        }
        else
        {
            return null;
        }

        return NewRequest(context, app, created, zone, null) with
        {
            Reason = reason,
            Rule = new AppRuleAtClose(budget, days, used),
            Message = KidMessageFactory.AppEnd(reason, context.Apps.BudgetText, app.DisplayName),
        };
    }

    private CloseRequest NewRequest(ScreenContext context, ObservedApp app, long created, TimeZoneInfo zone, BreakTimeEntryDto? entry)
    {
        var startedUtc = DateTimeOffset.FromFileTime(created).ToUniversalTime();
        return new CloseRequest(
            context.Observed.SessionId, context.Observed.AccountSid, context.UserName, app.ProgramPath, app.ProcessName, app.DisplayName,
            app.Processes, context.FirstOfRun ? BlockedStartTexts.AgentStartTrigger : BlockedStartTexts.AppStartTrigger, entry,
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
            if (state is not null)
            {
                // The session id was reused by another account: the old account's sequences are not this account's.
                registry.RemoveSession(sessionId);
            }

            state = new SessionState(accountSid);
            _sessions[sessionId] = state;
        }

        return state;
    }

    private sealed class SessionState(string accountSid)
    {
        public string AccountSid { get; } = accountSid;

        public long? Run { get; set; }
    }

    private sealed record ScreenContext(AppsObserved Observed, string UserName, bool FirstOfRun, AccountRules Breaks, AccountAppRules Apps);
}
