using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Used time per (account, program path, local day) for the budgets (ADR-015 §2, US-006 Decision 5): loaded from
/// <c>DailyUsage</c> when an account is controlled, then fed with the same tracker credits that <c>UsageService</c>
/// persists, so budgets and Reports never differ. Days before today are forgotten. Used by the accounting loop only.
/// </summary>
public sealed class TodayUsageLedger(
    IUsageRepository repository,
    IAppRuleService appRules,
    TimeProvider timeProvider,
    ILogger<TodayUsageLedger> logger)
{
    private readonly Dictionary<(string Sid, string Path, DateOnly Day), long> _seconds = [];
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
                foreach (var key in _seconds.Keys.ToList())
                {
                    if (key.Sid == upper)
                    {
                        _seconds.Remove(key);
                    }
                }
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
        try
        {
            rows = await repository.GetUsageAsync(sid, today, today, ct).ConfigureAwait(false);
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
    }

    private void Prune(DateOnly today)
    {
        foreach (var key in _seconds.Keys.ToList())
        {
            if (key.Day < today)
            {
                _seconds.Remove(key);
            }
        }
    }

    private static (string Sid, string Path, DateOnly Day) Key(string sid, string path, DateOnly day) =>
        (sid.ToUpperInvariant(), path.ToUpperInvariant(), day);

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone).DateTime);
}
