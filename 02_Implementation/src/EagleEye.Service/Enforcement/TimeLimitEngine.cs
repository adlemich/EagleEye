using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Time limits of running apps (ADR-015, US-006 Decisions 5 to 8), evaluated once per accounting tick after the tick's
/// credits are in the <see cref="TodayUsageLedger"/>. For every running app of a controlled account (reported, open in
/// the tracker, not ignored, not being closed): the end of its allowed time (<see cref="AppEndCalculator"/>), a grace
/// period after a parent change that moved the end earlier (<see cref="GracePolicy"/>), the warnings
/// (<see cref="WarningLedger"/>, at most one message per session and tick) and the close when no time is left (one
/// break-time message per session for all apps closed by a break). Never awaits: messages and close sequences run as
/// tasks. State per app ends when the app is no longer running; state per account when it is no longer controlled.
/// Used by the accounting loop only.
/// </summary>
public sealed class TimeLimitEngine(
    IBreakTimeService breaks,
    IAppRuleService appRules,
    IWarningSettingsService warningSettings,
    TodayUsageLedger ledger,
    SessionAppsView sessions,
    CloseRegistry registry,
    ICloseSequenceRunner runner,
    IKidMessenger messenger,
    EnforcementIgnoreList ignoreList,
    EnforcementLog log,
    TimeProvider timeProvider)
{
    private readonly WarningLedger _warnings = new();
    private readonly Dictionary<AppKey, (long Started, double Seconds)> _graces = [];
    private readonly Dictionary<AppKey, CloseEntry> _closed = [];
    private readonly Dictionary<string, (AccountRules Breaks, AccountAppRules Apps)> _rules = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<AppKey, AppEndKind?> _lastEnds = [];

    /// <summary>Evaluates every running app of the controlled accounts.</summary>
    /// <param name="controlledAccounts">User names by SID of the controlled accounts.</param>
    /// <param name="isOpen">Whether a program path is open for an account in the tracker.</param>
    public void Evaluate(IReadOnlyDictionary<string, string> controlledAccounts, Func<string, string, bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(controlledAccounts);
        ArgumentNullException.ThrowIfNull(isOpen);
        var nowUtc = timeProvider.GetUtcNow();
        var zone = timeProvider.LocalTimeZone;
        var local = BreakTimeSchedule.ToLocal(nowUtc, zone);
        _warnings.Prune(DateOnly.FromDateTime(local));
        var tick = new Tick(nowUtc, zone, local, warningSettings.Current, [], []);
        foreach (var session in sessions.Sessions.ToList())
        {
            if (controlledAccounts.TryGetValue(session.AccountSid, out var userName))
            {
                EvaluateSession(new SessionContext(tick, session, userName, breaks.Current.For(session.AccountSid), appRules.Current.For(session.AccountSid)), isOpen);
            }
        }

        foreach (var key in _graces.Keys.Where(k => !tick.Seen.ContainsKey(k)).ToList())
        {
            _graces.Remove(key);
        }

        foreach (var key in _closed.Keys.Where(k => !tick.Reported.Contains(k)).ToList())
        {
            _closed.Remove(key);
        }

        _lastEnds = tick.Seen;
        foreach (var sid in _rules.Keys.Where(s => !controlledAccounts.ContainsKey(s)).ToList())
        {
            _rules.Remove(sid);
        }

        foreach (var sid in controlledAccounts.Keys)
        {
            _rules[sid] = (breaks.Current.For(sid), appRules.Current.For(sid));
        }
    }

    private void EvaluateSession(SessionContext context, Func<string, string, bool> isOpen)
    {
        var sid = context.Session.AccountSid;
        context.Old = _rules.TryGetValue(sid, out var old) && (!ReferenceEquals(old.Breaks, context.Breaks) || !ReferenceEquals(old.Apps, context.Apps))
            ? old
            : null;
        foreach (var app in context.Session.Apps)
        {
            var key = new AppKey(context.Session.SessionId, app.ProgramPath.ToUpperInvariant());
            context.Tick.Reported.Add(key);
            if (IsRunning(context, app, key, isOpen))
            {
                EvaluateApp(context, app, key);
            }
        }

        if (context.BreakGraces.Count > 0)
        {
            AddBreakGraceMessage(context);
        }

        context.Outbox.Send(messenger, context.Session.SessionId);
        if (context.BreakCloses.Count > 0)
        {
            CloseForBreak(context);
        }
    }

    private bool IsRunning(SessionContext context, ObservedApp app, AppKey key, Func<string, string, bool> isOpen)
    {
        if (app.Processes.Count == 0 || ignoreList.IsIgnored(app.ProgramPath) || !isOpen(context.Session.AccountSid, app.ProgramPath)
            || registry.IsClosing(context.Session.SessionId, app.ProgramPath))
        {
            return false;
        }

        // A closed app may still be reported until the agent's next report: its known processes are not a new run.
        return !(_closed.TryGetValue(key, out var closed) && app.Processes.All(p => closed.Known.Contains((p.Pid, p.Created))));
    }

    private void EvaluateApp(SessionContext context, ObservedApp app, AppKey key)
    {
        var tick = context.Tick;
        var rule = context.Apps.ByPath.GetValueOrDefault(app.ProgramPath);
        var used = ledger.UsedSeconds(context.Session.AccountSid, app.ProgramPath, DateOnly.FromDateTime(tick.Local));
        var end = AppEndCalculator.Compute(rule, used, context.Breaks.Entries, tick.NowUtc, tick.Zone);
        var natural = end?.SecondsLeft ?? double.PositiveInfinity;
        var firstSeen = !_lastEnds.ContainsKey(key);
        tick.Seen[key] = end?.Kind;
        var afterGrace = false;
        var effective = natural;
        if (_graces.TryGetValue(key, out var grace))
        {
            var graceLeft = grace.Seconds - timeProvider.GetElapsedTime(grace.Started).TotalSeconds;
            if (natural > 0 && natural >= graceLeft)
            {
                // The natural time left (e.g. after a raised budget) is at least the grace: the grace ends.
                _graces.Remove(key);
            }
            else if (graceLeft > 0)
            {
                return;
            }
            else
            {
                afterGrace = true;
            }
        }
        else if (context.Old is { } old && !firstSeen && end is not null && StartsGrace(context, old, app, used, natural))
        {
            StartGrace(context, app, key, end);
            return;
        }

        if (effective > 0)
        {
            Warn(context, app, end, effective);
        }
        else
        {
            // end is not null: no time left means an end within the horizon.
            Close(context, app, key, end!, afterGrace, new AppRuleAtClose(rule?.BudgetMinutes ?? 0, rule?.Days ?? BreakTimeDays.None, used));
        }
    }

    private bool StartsGrace(SessionContext context, (AccountRules Breaks, AccountAppRules Apps) old, ObservedApp app, long used, double natural)
    {
        var oldEnd = AppEndCalculator.Compute(old.Apps.ByPath.GetValueOrDefault(app.ProgramPath), used, old.Breaks.Entries, context.Tick.NowUtc, context.Tick.Zone);
        return GracePolicy.Starts(natural, oldEnd?.SecondsLeft ?? double.PositiveInfinity, context.Tick.Settings.FinalMinutes);
    }

    private void StartGrace(SessionContext context, ObservedApp app, AppKey key, AppEnd end)
    {
        var settings = context.Tick.Settings;
        var seconds = GracePolicy.Threshold(settings.FinalMinutes);
        _graces[key] = (timeProvider.GetTimestamp(), seconds);
        _warnings.MarkShown(WarningKeyOf(context, app, end), WarningLevel.Final);
        if (end.Kind == AppEndKind.Break)
        {
            context.BreakGraces.Add(end);
            return;
        }

        var closes = context.Tick.Local.AddSeconds(seconds);
        context.Outbox.Add(
            KidMessageFactory.AppGrace(settings, app.DisplayName),
            state => log.AppGrace(context.UserName, app.DisplayName, app.ProcessName, end.Kind, settings.FinalMinutes, closes, state));
    }

    private void AddBreakGraceMessage(SessionContext context)
    {
        var settings = context.Tick.Settings;
        var end = context.BreakGraces[0];
        var entry = end.Entry!; // A break end always carries its entry.
        var begins = end.BreakStartLocal!.Value;
        var closes = context.Tick.Local.AddSeconds(GracePolicy.Threshold(settings.FinalMinutes));
        var count = context.BreakGraces.Count;
        context.Outbox.Add(
            KidMessageFactory.BreakGrace(settings, entry.StartMinute),
            state => log.BreakGrace(context.UserName, entry, begins, count, settings.FinalMinutes, closes, state));
    }

    private void Warn(SessionContext context, ObservedApp app, AppEnd? end, double left)
    {
        var settings = context.Tick.Settings;
        if (end is null || LevelFor(left, settings) is not { } level)
        {
            return;
        }

        var key = WarningKeyOf(context, app, end);
        if (_warnings.IsShown(key, level))
        {
            return;
        }

        _warnings.MarkShown(key, level);
        var minutes = KidMessageFactory.Minutes(left);
        if (end.Kind == AppEndKind.Break)
        {
            var entry = end.Entry!; // A break end always carries its entry.
            var begins = end.BreakStartLocal!.Value;
            context.Outbox.Add(
                KidMessageFactory.BreakWarning(level, settings, entry.StartMinute, left),
                state => log.BreakWarning(context.UserName, entry, begins, level, minutes, state));
        }
        else
        {
            context.Outbox.Add(
                KidMessageFactory.AppWarning(level, settings, app.DisplayName, left),
                state => log.AppWarning(context.UserName, app.DisplayName, app.ProcessName, level, minutes, end.Kind, state));
        }
    }

    private void Close(SessionContext context, ObservedApp app, AppKey key, AppEnd end, bool afterGrace, AppRuleAtClose rule)
    {
        _graces.Remove(key);
        var request = NewRequest(context, app, end, afterGrace) with { Rule = end.Kind == AppEndKind.Break ? null : rule };
        if (end.Kind == AppEndKind.Break)
        {
            context.BreakCloses.Add((app, key, request));
            return;
        }

        var reason = request.Reason;
        Start(context, app, key, request with { Message = KidMessageFactory.AppEnd(reason, context.Apps.BudgetText, app.DisplayName) });
    }

    /// <summary>A break begins: one break-time message per session for all its apps (AC-29).</summary>
    private void CloseForBreak(SessionContext context)
    {
        var entry = context.BreakCloses[0].Request.Entry!; // A break close always carries its entry.
        var message = messenger.ShowAsync(context.Session.SessionId, KidMessageFactory.BreakTime(context.Breaks.CustomText, entry), CancellationToken.None);
        foreach (var (app, key, request) in context.BreakCloses)
        {
            Start(context, app, key, request with { SharedMessageState = message });
        }
    }

    private void Start(SessionContext context, ObservedApp app, AppKey key, CloseRequest request)
    {
        var entry = new CloseEntry(runner.Start(request), false, [.. app.Processes.Select(p => (p.Pid, p.Created))]);
        registry.Add(context.Session.SessionId, app.ProgramPath, entry);
        _closed[key] = entry;
    }

    private CloseRequest NewRequest(SessionContext context, ObservedApp app, AppEnd end, bool afterGrace)
    {
        var tick = context.Tick;
        var key = new AppKey(context.Session.SessionId, app.ProgramPath.ToUpperInvariant());
        var trigger = afterGrace ? BlockedStartTexts.GraceEndedTrigger : end.Kind switch
        {
            AppEndKind.Break => BlockedStartTexts.BreakBeganTrigger,
            AppEndKind.Budget => BlockedStartTexts.BudgetRanOutTrigger,
            AppEndKind.Midnight => BlockedStartTexts.MidnightTrigger,
            _ => _lastEnds.GetValueOrDefault(key) == AppEndKind.Midnight ? BlockedStartTexts.MidnightTrigger : BlockedStartTexts.RulesChangedTrigger,
        };
        var reason = end.Kind switch
        {
            AppEndKind.Break => CloseReasons.BreakTime,
            AppEndKind.Budget => CloseReasons.BudgetUsedUp,
            _ => CloseReasons.NotAllowedToday,
        };
        var startedUtc = DateTimeOffset.FromFileTime(app.Processes.Min(p => p.Created)).ToUniversalTime();
        return new CloseRequest(
            context.Session.SessionId, context.Session.AccountSid, context.UserName, app.ProgramPath, app.ProcessName, app.DisplayName,
            app.Processes, trigger, end.Entry, startedUtc, BreakTimeSchedule.ToLocal(startedUtc, tick.Zone), tick.NowUtc)
        {
            Kind = CloseKinds.RunningApp,
            Reason = reason,
            AfterGrace = afterGrace,
        };
    }

    private static WarningKey WarningKeyOf(SessionContext context, ObservedApp app, AppEnd end) =>
        end.Kind == AppEndKind.Break
            ? WarningLedger.BreakKey(context.Session.SessionId, end.BreakStartLocal!.Value) // A break end always carries its start.
            : WarningLedger.AppKey(context.Session.AccountSid, app.ProgramPath, DateOnly.FromDateTime(context.Tick.Local));

    private static WarningLevel? LevelFor(double left, WarningSettings settings) =>
        left <= GracePolicy.Threshold(settings.FinalMinutes) ? WarningLevel.Final
        : left <= GracePolicy.Threshold(settings.EarlyMinutes) ? WarningLevel.Early
        : null;

    private readonly record struct AppKey(int SessionId, string PathUpper);

    private sealed record Tick(
        DateTimeOffset NowUtc, TimeZoneInfo Zone, DateTime Local, WarningSettings Settings, Dictionary<AppKey, AppEndKind?> Seen, HashSet<AppKey> Reported);

    private sealed class SessionContext(Tick tick, SessionApps session, string userName, AccountRules breaks, AccountAppRules apps)
    {
        public Tick Tick { get; } = tick;

        public SessionApps Session { get; } = session;

        public string UserName { get; } = userName;

        public AccountRules Breaks { get; } = breaks;

        public AccountAppRules Apps { get; } = apps;

        public (AccountRules Breaks, AccountAppRules Apps)? Old { get; set; }

        public MessageOutbox Outbox { get; } = new();

        public List<AppEnd> BreakGraces { get; } = [];

        public List<(ObservedApp App, AppKey Key, CloseRequest Request)> BreakCloses { get; } = [];
    }
}
