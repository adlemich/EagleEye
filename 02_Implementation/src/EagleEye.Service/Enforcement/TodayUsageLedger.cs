using EagleEye.Service.Data;
using EagleEye.Service.Groups;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Used time per (account, program path, local day) for the budgets (ADR-015 §2, US-006 Decision 5): loaded from
/// <c>DailyUsage</c> when an account is controlled, then fed with the same tracker credits that <c>UsageService</c>
/// persists, so budgets and Reports never differ. Also keeps today's activity spans per (account, program path) for the group used
/// time (US-008, ADR-021 §3), loaded and fed the same way. Days before today are forgotten. Used by the accounting loop only.
/// </summary>
public sealed class TodayUsageLedger(
    IUsageRepository repository,
    IAppRuleService appRules,
    TimeProvider timeProvider,
    ILogger<TodayUsageLedger> logger)
{
    private readonly Dictionary<(string Sid, string Path, DateOnly Day), long> _seconds = [];
    private readonly Dictionary<(string Sid, string Path, DateOnly Day), Dictionary<DateTimeOffset, DateTimeOffset>> _spans = [];
    private readonly HashSet<string> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadFailureLogged;

    /// <summary>Adds the credits of accounts that are loaded (newly loaded accounts read them from the database).</summary>
    public void Apply(IReadOnlyList<UsageCredit> credits)
    {
        ArgumentNullException.ThrowIfNull(credits);
        var today = Today();
        foreach (var credit in credits.Where(c => c.Day >= today && _loaded.Contains(c.AccountSid)))
        {
            var key = Key(credit.AccountSid, credit.ProgramPath, credit.Day);
            _seconds[key] = _seconds.GetValueOrDefault(key) + credit.Seconds;
        }

        Prune(today);
    }

    /// <summary>Adds the activity spans of loaded accounts (US-008, ADR-021 §3): a span with a known start gets its new end.</summary>
    public void ApplySpans(IReadOnlyList<SpanUpdate> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        var today = Today();
        foreach (var span in spans.Where(s => s.Day >= today && _loaded.Contains(s.AccountSid)))
        {
            AddSpan(Key(span.AccountSid, span.ProgramPath, span.Day), span.StartUtc, span.EndUtc);
        }
    }

    /// <summary>
    /// Loads today's usage of controlled accounts that are not loaded yet and forgets accounts that are no longer
    /// controlled. A failed load is logged once; the account then counts from 0 until the next service start.
    /// </summary>
    public async Task EnsureLoadedAsync(IReadOnlyCollection<string> controlledSids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(controlledSids);
        var controlled = new HashSet<string>(controlledSids, StringComparer.OrdinalIgnoreCase);
        foreach (var sid in _loaded.ToList())
        {
            if (!controlled.Contains(sid))
            {
                _loaded.Remove(sid);
                var upper = sid.ToUpperInvariant();
                RemoveWhere(key => key.Sid == upper);
            }
        }

        var today = Today();
        foreach (var sid in controlled)
        {
            if (_loaded.Add(sid))
            {
                await LoadAsync(sid, today, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The group used time (TC-026): the union of today's spans of the member paths, in whole seconds (US-008 AC-19).</summary>
    public long GroupUsedSeconds(string accountSid, IEnumerable<string> memberPaths, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(memberPaths);
        return SpanUnion.Seconds(memberPaths.SelectMany(path =>
            _spans.TryGetValue(Key(accountSid, path, day), out var spans) ? spans.Select(s => (s.Key, s.Value)) : []));
    }

    /// <summary>The used seconds of the app on the day (0 if none).</summary>
    public long UsedSeconds(string accountSid, string programPath, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(programPath);
        return _seconds.GetValueOrDefault(Key(accountSid, programPath, day));
    }

    private async Task LoadAsync(string sid, DateOnly today, CancellationToken ct)
    {
        IReadOnlyList<DayAppUsage> rows;
        IReadOnlyList<StoredSpan> spans;
        try
        {
            rows = await repository.GetUsageAsync(sid, today, today, ct).ConfigureAwait(false);
            spans = await repository.GetSpansAsync(sid, today, today, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Persistence boundary: budgets count from 0 for this account until the next start (logged once).
            if (!_loadFailureLogged)
            {
                _loadFailureLogged = true;
                logger.LogWarning(ex, "Loading today's usage for the budgets failed; budgets use 0 used seconds until the next service start.");
            }

            return;
        }

        var apps = appRules.Current.For(sid).Apps;
        foreach (var row in rows)
        {
            if (apps.TryGetValue(row.AppId, out var app))
            {
                var key = Key(sid, app.ProgramPath, row.Day);
                _seconds[key] = _seconds.GetValueOrDefault(key) + row.Seconds;
            }
        }

        foreach (var span in spans)
        {
            AddSpan(Key(sid, span.ProgramPath, span.Day), span.StartUtc, span.EndUtc);
        }
    }

    private void Prune(DateOnly today) => RemoveWhere(key => key.Day < today);

    private void RemoveWhere(Func<(string Sid, string Path, DateOnly Day), bool> remove)
    {
        foreach (var key in _seconds.Keys.Concat(_spans.Keys).Where(remove).ToList())
        {
            _seconds.Remove(key);
            _spans.Remove(key);
        }
    }

    /// <summary>Adds or extends a span; the start is kept in milliseconds, as stored, so a span loaded from the database and its later
    /// updates from the tracker are one entry.</summary>
    private void AddSpan((string Sid, string Path, DateOnly Day) key, DateTimeOffset startUtc, DateTimeOffset end)
    {
        var start = DateTimeOffset.FromUnixTimeMilliseconds(startUtc.ToUnixTimeMilliseconds());
        if (!_spans.TryGetValue(key, out var spans))
        {
            spans = [];
            _spans[key] = spans;
        }

        spans[start] = spans.TryGetValue(start, out var known) && known > end ? known : end;
    }

    private static (string Sid, string Path, DateOnly Day) Key(string sid, string path, DateOnly day) =>
        (sid.ToUpperInvariant(), path.ToUpperInvariant(), day);

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone).DateTime);
}
