namespace EagleEye.Service.Statistics;

/// <summary>
/// The accounting state machine of ADR-012 (US-004 AC-6, AC-10, AC-12 to AC-16). Not thread-safe: it is used
/// only by the accounting loop. Per account it keeps the open apps (one per program path, AC-6) with their
/// instance and the monotonic time they are credited up to. Every mutating call first credits up to now with
/// the activity as it was (<see cref="Advance"/>); <see cref="SetActivity"/> sets the activity for what follows.
/// Durations use the monotonic clock only; the wall clock places the time on local days (AC-14, AC-16).
/// </summary>
public sealed class UsageTracker(TimeProvider timeProvider, ILogger<UsageTracker> logger)
{
    /// <summary>Longer intervals between two crediting points are not counted (sleep, suspension, hangs).</summary>
    public static readonly TimeSpan GapLimit = TimeSpan.FromSeconds(15);

    /// <summary>An app that disappears and reappears within this time continues its instance (T-12).</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(5);

    private readonly Dictionary<int, SessionApps> _sessions = [];
    private readonly Dictionary<string, AccountState> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly UsageAccumulator _usage = new();
    private readonly List<InstanceEvent> _events = [];
    private long _lastAdvance = timeProvider.GetTimestamp();
    private long _nextKey;
    private bool _suspended;

    /// <summary>Credits all active open apps up to now and ends instances whose app is gone for 5 s.</summary>
    public void Advance()
    {
        var now = timeProvider.GetTimestamp();
        var wallNow = timeProvider.GetUtcNow();
        var elapsed = timeProvider.GetElapsedTime(_lastAdvance, now);
        var countable = !_suspended && elapsed <= GapLimit;
        var gapLogged = false;
        foreach (var (sid, account) in _accounts)
        {
            foreach (var app in account.Open.Values.ToList())
            {
                if (app.GoneSince is null && account.Active && !_suspended)
                {
                    if (countable)
                    {
                        Credit(sid, app, timeProvider.GetElapsedTime(app.CreditedUpTo, now), wallNow);
                    }
                    else if (!gapLogged)
                    {
                        gapLogged = true;
                        logger.LogInformation(
                            "Usage accounting paused for {Seconds} s (sleep, suspension or delay); this time is not counted.",
                            (long)elapsed.TotalSeconds);
                    }
                }

                app.CreditedUpTo = now;
                if (app.GoneSince is { } gone && timeProvider.GetElapsedTime(gone, now) >= MergeWindow)
                {
                    End(sid, account, app, wallNow - timeProvider.GetElapsedTime(gone, now), EndReasons.Closed);
                }
            }
        }

        _lastAdvance = now;
    }

    /// <summary>Sets for every account whether its apps count as active from now on (AC-12).</summary>
    public void SetActivity(Func<string, bool> isActive)
    {
        ArgumentNullException.ThrowIfNull(isActive);
        foreach (var (sid, account) in _accounts)
        {
            account.Active = isActive(sid);
        }
    }

    /// <summary>The latest apps of a session (its agent's report).</summary>
    public void ObserveApps(int sessionId, string accountSid, IReadOnlyList<ObservedApp> apps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentNullException.ThrowIfNull(apps);
        Advance();
        if (_sessions.TryGetValue(sessionId, out var previous) && !StringComparer.OrdinalIgnoreCase.Equals(previous.Sid, accountSid))
        {
            RemoveSessionCore(sessionId, EndReasons.SessionEnded);
        }

        var session = new SessionApps(accountSid);
        foreach (var app in apps)
        {
            session.Apps[app.ProgramPath] = app;
        }

        _sessions[sessionId] = session;
        Reconcile(accountSid, immediateEndReason: null);
    }

    /// <summary>The session was logged off: its apps end at once with "session ended".</summary>
    public void RemoveSession(int sessionId)
    {
        Advance();
        RemoveSessionCore(sessionId, EndReasons.SessionEnded);
    }

    /// <summary>Recording stops for the account (unticked, became admin): all its instances end.</summary>
    public void StopAccount(string accountSid, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Advance();
        foreach (var (id, _) in _sessions.Where(s => StringComparer.OrdinalIgnoreCase.Equals(s.Value.Sid, accountSid)).ToList())
        {
            _sessions.Remove(id);
        }

        Reconcile(accountSid, reason);
        _accounts.Remove(accountSid);
    }

    /// <summary>Ends everything (service stopping).</summary>
    public void StopAll(string reason)
    {
        foreach (var sid in _accounts.Keys.ToList())
        {
            StopAccount(sid, reason);
        }
    }

    /// <summary>The PC goes to sleep: nothing is credited until <see cref="Resume"/> (AC-15).</summary>
    public void Suspend()
    {
        Advance();
        _suspended = true;
    }

    /// <summary>The PC resumed: crediting restarts from now; the sleep is never counted.</summary>
    public void Resume()
    {
        Advance();
        _suspended = false;
    }

    /// <summary>
    /// Returns the instance events since the last call and, when <paramref name="includeUsage"/> is set (ticks),
    /// the whole seconds credited and the "last seen" time of every open instance.
    /// </summary>
    public TrackerOutput TakeOutput(bool includeUsage)
    {
        var events = _events.ToList();
        _events.Clear();
        if (!includeUsage)
        {
            return new TrackerOutput(events, [], []);
        }

        var wallNow = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(wallNow, timeProvider.LocalTimeZone).DateTime);
        var seen = _accounts.Values
            .SelectMany(a => a.Open.Values)
            .Where(app => app.GoneSince is null)
            .Select(app => new InstanceSeen(app.Key, wallNow))
            .ToList();
        return new TrackerOutput(events, _usage.Emit(today.AddDays(-1)), seen);
    }

    private void RemoveSessionCore(int sessionId, string reason)
    {
        if (_sessions.Remove(sessionId, out var session))
        {
            Reconcile(session.Sid, reason);
        }
    }

    /// <summary>Opens apps that appeared and marks or ends apps that are gone from every session of the account.</summary>
    private void Reconcile(string sid, string? immediateEndReason)
    {
        var present = new Dictionary<string, ObservedApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in _sessions.Values.Where(s => StringComparer.OrdinalIgnoreCase.Equals(s.Sid, sid)))
        {
            foreach (var (path, app) in session.Apps)
            {
                present.TryAdd(path, app);
            }
        }

        if (!_accounts.TryGetValue(sid, out var account))
        {
            account = new AccountState();
            _accounts[sid] = account;
        }

        var now = timeProvider.GetTimestamp();
        var wallNow = timeProvider.GetUtcNow();
        foreach (var app in account.Open.Values.Where(a => !present.ContainsKey(a.Path)).ToList())
        {
            if (immediateEndReason is not null)
            {
                var endedUtc = app.GoneSince is { } gone ? wallNow - timeProvider.GetElapsedTime(gone, now) : wallNow;
                End(sid, account, app, endedUtc, app.GoneSince is null ? immediateEndReason : EndReasons.Closed);
            }
            else
            {
                app.GoneSince ??= now;
            }
        }

        foreach (var (path, observed) in present)
        {
            if (account.Open.TryGetValue(path, out var open))
            {
                open.GoneSince = null;
                continue;
            }

            var started = new OpenApp(++_nextKey, path, observed.ProcessName, observed.DisplayName, wallNow) { CreditedUpTo = now };
            account.Open[path] = started;
            _events.Add(new InstanceStarted(started.Key, sid, path, started.ProcessName, started.DisplayName, wallNow));
        }
    }

    private void Credit(string sid, OpenApp app, TimeSpan length, DateTimeOffset wallNow)
    {
        foreach (var (day, part) in DaySplitter.Split(wallNow, length, timeProvider.LocalTimeZone))
        {
            _usage.Add(sid, app.Path, day, part);
        }
    }

    private void End(string sid, AccountState account, OpenApp app, DateTimeOffset endedUtc, string reason)
    {
        account.Open.Remove(app.Path);
        _events.Add(new InstanceEnded(app.Key, sid, app.Path, app.ProcessName, app.DisplayName, app.StartedUtc, endedUtc, reason));
    }

    private sealed record SessionApps(string Sid)
    {
        public Dictionary<string, ObservedApp> Apps { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class AccountState
    {
        public Dictionary<string, OpenApp> Open { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Active { get; set; }
    }

    private sealed class OpenApp(long key, string path, string processName, string displayName, DateTimeOffset startedUtc)
    {
        public long Key { get; } = key;

        public string Path { get; } = path;

        public string ProcessName { get; } = processName;

        public string DisplayName { get; } = displayName;

        public DateTimeOffset StartedUtc { get; } = startedUtc;

        public long CreditedUpTo { get; set; }

        public long? GoneSince { get; set; }
    }
}
