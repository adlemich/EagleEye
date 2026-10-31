using AppKey = (int SessionId, string PathUpper);
using SubjectKey = (int SessionId, string Key);
using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Groups;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Time limits of running apps (ADR-015, ADR-021 §4, US-006 Decisions 5 to 8, US-008 Decision 6), evaluated once per
/// accounting tick after the tick's credits and spans are in the <see cref="TodayUsageLedger"/>. The running apps of a
/// controlled account (reported, open in the tracker, not ignored, not being closed) are grouped by their
/// <see cref="LimitSubject"/> (the app, or its app group). Per subject: the end of its allowed time
/// (<see cref="AppEndCalculator"/>), a grace period after a parent change that moved the end earlier for one of its apps
/// (<see cref="GracePolicy"/>), the warnings (<see cref="WarningLedger"/>, at most one message per session and tick) and the
/// close of all its running apps with one message when no time is left (one break-time message per session for all apps
/// closed by a break). Never awaits: messages and close sequences run as tasks. State per subject ends when none of its apps
/// runs; state per account when it is no longer controlled. Used by the accounting loop only.
/// </summary>
public sealed class TimeLimitEngine(
    IBreakTimeService breaks,
    IAppRuleService appRules,
    IAppGroupsView groups,
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
    private readonly Dictionary<SubjectKey, (long Started, double Seconds)> _graces = [];
    private readonly Dictionary<AppKey, CloseEntry> _closed = [];
    private readonly Dictionary<string, RuleSet> _rules = new(StringComparer.OrdinalIgnoreCase);
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
        var tick = new Tick(nowUtc, zone, local, warningSettings.Current, [], [], []);
        var groupsNow = groups.Current;
        foreach (var session in sessions.Sessions.ToList())
        {
            if (controlledAccounts.TryGetValue(session.AccountSid, out var userName))
            {
                EvaluateSession(new SessionContext(tick, session, userName, RulesOf(session.AccountSid, groupsNow)), isOpen);
            }
        }

        Forget(_graces, tick.Subjects.Contains);
        Forget(_closed, tick.Reported.Contains);
        _lastEnds = tick.Seen;
        _rules.Clear();
        foreach (var sid in controlledAccounts.Keys)
        {
            _rules[sid] = RulesOf(sid, groupsNow);
        }
    }

    private RuleSet RulesOf(string sid, AppGroupsSnapshot groupsNow) => new(breaks.Current.For(sid), appRules.Current.For(sid), groupsNow);

    private void EvaluateSession(SessionContext context, Func<string, string, bool> isOpen)
    {
        var sid = context.Session.AccountSid;
        context.Old = _rules.TryGetValue(sid, out var old) && old.DiffersFrom(context.Rules) ? old : null;
        var day = DateOnly.FromDateTime(context.Tick.Local);
        var subjects = new Dictionary<string, (LimitSubject Subject, List<(ObservedApp App, AppKey Key)> Apps)>(StringComparer.Ordinal);
        foreach (var app in context.Session.Apps)
        {
            var key = new AppKey(context.Session.SessionId, app.ProgramPath.ToUpperInvariant());
            context.Tick.Reported.Add(key);
            if (IsRunning(context, app, key, isOpen))
            {
                var subject = LimitSubjectResolver.Resolve(ledger, sid, app.ProgramPath, app.DisplayName, context.Rules.Apps, context.Rules.Groups, day);
                if (!subjects.TryGetValue(subject.Key, out var entry))
                {
                    entry = (subject, []);
                    subjects[subject.Key] = entry;
                }

                entry.Apps.Add((app, key));
            }
        }

        foreach (var (subject, apps) in subjects.Values)
        {
            EvaluateSubject(context, subject, apps);
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

    private void EvaluateSubject(SessionContext context, LimitSubject subject, List<(ObservedApp App, AppKey Key)> apps)
    {
        var tick = context.Tick;
        var end = AppEndCalculator.Compute(subject.Rule, subject.UsedSeconds, context.Rules.Breaks.Entries, tick.NowUtc, tick.Zone);
        var natural = end?.SecondsLeft ?? double.PositiveInfinity;
        var graceKey = new SubjectKey(context.Session.SessionId, subject.Key);
        tick.Subjects.Add(graceKey);
        foreach (var (_, key) in apps)
        {
            tick.Seen[key] = end?.Kind;
        }

        var afterGrace = false;
        if (_graces.TryGetValue(graceKey, out var grace))
        {
            var graceLeft = grace.Seconds - timeProvider.GetElapsedTime(grace.Started).TotalSeconds;
            if (natural > 0 && natural >= graceLeft)
            {
                // The natural time left (e.g. after a raised budget) is at least the grace: the grace ends.
                _graces.Remove(graceKey);
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
        else if (context.Old is { } old && end is not null && apps.Any(a => _lastEnds.ContainsKey(a.Key) && StartsGrace(context, old, a.App, natural)))
        {
            StartGrace(context, subject, apps, graceKey, end);
            return;
        }

        if (natural > 0)
        {
            Warn(context, subject, apps, end, natural);
        }
        else
        {
            // end is not null: no time left means an end within the horizon.
            Close(context, subject, apps, graceKey, end!, afterGrace);
        }
    }

    /// <summary>The app's end under the previous rules, groups and members (its previous subject) vs. the new end (ADR-021 §4).</summary>
    private bool StartsGrace(SessionContext context, RuleSet old, ObservedApp app, double natural)
    {
        var tick = context.Tick;
        var before = LimitSubjectResolver.Resolve(
            ledger, context.Session.AccountSid, app.ProgramPath, app.DisplayName, old.Apps, old.Groups, DateOnly.FromDateTime(tick.Local));
        var oldEnd = AppEndCalculator.Compute(before.Rule, before.UsedSeconds, old.Breaks.Entries, tick.NowUtc, tick.Zone);
        return GracePolicy.Starts(natural, oldEnd?.SecondsLeft ?? double.PositiveInfinity, tick.Settings.FinalMinutes);
    }

    private void StartGrace(SessionContext context, LimitSubject subject, List<(ObservedApp App, AppKey Key)> apps, SubjectKey graceKey, AppEnd end)
    {
        var settings = context.Tick.Settings;
        var seconds = GracePolicy.Threshold(settings.FinalMinutes);
        _graces[graceKey] = (timeProvider.GetTimestamp(), seconds);
        _warnings.MarkShown(WarningKeyOf(context, subject, end), WarningLevel.Final);
        if (end.Kind == AppEndKind.Break)
        {
            context.BreakGraces.AddRange(apps.Select(_ => end));
            return;
        }

        var closes = context.Tick.Local.AddSeconds(seconds);
        var what = Describe(subject, apps);
        context.Outbox.Add(
            KidMessageFactory.AppGrace(settings, subject.Name),
            state => log.AppGrace(context.UserName, what, end.Kind, settings.FinalMinutes, closes, state));
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

    private void Warn(SessionContext context, LimitSubject subject, List<(ObservedApp App, AppKey Key)> apps, AppEnd? end, double left)
    {
        var settings = context.Tick.Settings;
        if (end is null || LevelFor(left, settings) is not { } level)
        {
            return;
        }

        var key = WarningKeyOf(context, subject, end);
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
            var what = Describe(subject, apps);
            context.Outbox.Add(
                KidMessageFactory.AppWarning(level, settings, subject.Name, left),
                state => log.AppWarning(context.UserName, what, level, minutes, end.Kind, state));
        }
    }

    /// <summary>Closes all running apps of the subject; a group with several running apps gets one shared message (AC-27).</summary>
    private void Close(SessionContext context, LimitSubject subject, List<(ObservedApp App, AppKey Key)> apps, SubjectKey graceKey, AppEnd end, bool afterGrace)
    {
        _graces.Remove(graceKey);
        var rule = end.Kind == AppEndKind.Break
            ? null
            : new AppRuleAtClose(subject.Rule.BudgetMinutes, subject.Rule.Days, subject.UsedSeconds, subject.GroupId, subject.GroupId is null ? null : subject.Name);
        var closes = apps.Select(a => (a.App, a.Key, Request: NewRequest(context, a.App, end, afterGrace) with { Rule = rule })).ToList();
        if (end.Kind == AppEndKind.Break)
        {
            context.BreakCloses.AddRange(closes);
            return;
        }

        var message = KidMessageFactory.AppEnd(closes[0].Request.Reason, context.Rules.Apps.BudgetText, subject.Name);
        if (closes.Count == 1)
        {
            Start(context, closes[0].App, closes[0].Key, closes[0].Request with { Message = message });
            return;
        }

        var shared = messenger.ShowAsync(context.Session.SessionId, message, CancellationToken.None);
        foreach (var (app, key, request) in closes)
        {
            Start(context, app, key, request with { SharedMessageState = shared });
        }
    }

    /// <summary>A break begins: one break-time message per session for all its apps (AC-29).</summary>
    private void CloseForBreak(SessionContext context)
    {
        var entry = context.BreakCloses[0].Request.Entry!; // A break close always carries its entry.
        var message = messenger.ShowAsync(context.Session.SessionId, KidMessageFactory.BreakTime(context.Rules.Breaks.CustomText, entry), CancellationToken.None);
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

    private static string Describe(LimitSubject subject, List<(ObservedApp App, AppKey Key)> apps) =>
        EnforcementLog.Subject(subject.GroupId is null ? null : subject.Name, apps.Select(a => (a.App.DisplayName, a.App.ProcessName)));

    private static WarningKey WarningKeyOf(SessionContext context, LimitSubject subject, AppEnd end) =>
        end.Kind == AppEndKind.Break
            ? WarningLedger.BreakKey(context.Session.SessionId, end.BreakStartLocal!.Value) // A break end always carries its start.
            : WarningLedger.SubjectKey(context.Session.AccountSid, subject, DateOnly.FromDateTime(context.Tick.Local));

    private static WarningLevel? LevelFor(double left, WarningSettings settings) =>
        left <= GracePolicy.Threshold(settings.FinalMinutes) ? WarningLevel.Final
        : left <= GracePolicy.Threshold(settings.EarlyMinutes) ? WarningLevel.Early
        : null;

    /// <summary>Removes the entries whose key is not kept.</summary>
    private static void Forget<TKey, TValue>(Dictionary<TKey, TValue> entries, Func<TKey, bool> keep)
        where TKey : notnull
    {
        foreach (var key in entries.Keys.ToList())
        {
            if (!keep(key))
            {
                entries.Remove(key);
            }
        }
    }

    /// <summary>The rules of an account at one tick: break times, app rules and the (global) app groups with their rules.</summary>
    private sealed record RuleSet(AccountRules Breaks, AccountAppRules Apps, AppGroupsSnapshot Groups)
    {
        /// <summary>Whether a parent change replaced any of the snapshots (a grace period may start).</summary>
        public bool DiffersFrom(RuleSet other) =>
            !ReferenceEquals(Breaks, other.Breaks) || !ReferenceEquals(Apps, other.Apps) || !ReferenceEquals(Groups, other.Groups);
    }

    private sealed record Tick(
        DateTimeOffset NowUtc, TimeZoneInfo Zone, DateTime Local, WarningSettings Settings, Dictionary<AppKey, AppEndKind?> Seen, HashSet<AppKey> Reported,
        HashSet<SubjectKey> Subjects);

    private sealed class SessionContext(Tick tick, SessionApps session, string userName, RuleSet rules)
    {
        public Tick Tick { get; } = tick;

        public SessionApps Session { get; } = session;

        public string UserName { get; } = userName;

        public RuleSet Rules { get; } = rules;

        public RuleSet? Old { get; set; }

        public MessageOutbox Outbox { get; } = new();

        public List<AppEnd> BreakGraces { get; } = [];

        public List<(ObservedApp App, AppKey Key, CloseRequest Request)> BreakCloses { get; } = [];
    }
}
