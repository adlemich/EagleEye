using System.Globalization;
using EagleEye.Service.Data;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Statistics;

/// <summary>
/// State owner of the usage areas (ADR-010 §9, ADR-012 §5 and §6). One lock serializes everything. App records and
/// instance starts/ends are written at once; credited seconds and "last seen" times are written once per tick in
/// one transaction (crash loss ≤ 5 s, AC-15). A failed write keeps the data in memory and retries with the next
/// tick. After a successful write, each changed (account, day) is broadcast once, with the account's revision
/// incremented (per-account counter, ADR-012 §6).
/// </summary>
public sealed class UsageService(
    IUsageRepository repository,
    IUsageBroadcaster broadcaster,
    IUserAccountService userAccounts,
    TimeProvider timeProvider,
    ILogger<UsageService> logger) : IUsageService, IAccountDataPurger, IDisposable
{
    /// <summary>Days kept: today and the 89 days before (AC-11, AC-19).</summary>
    public const int RetentionDays = 90;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly UsageHistoryLog _history = new(logger, new InstanceLogLimiter(timeProvider));
    private readonly Dictionary<string, long> _appIds = new(StringComparer.Ordinal);
    private readonly Dictionary<long, long> _instanceIds = [];
    private readonly Dictionary<(long AppId, DateOnly Day), long> _pendingSeconds = [];
    private readonly Dictionary<long, DateTimeOffset> _pendingSeen = [];
    private readonly HashSet<(string Sid, DateOnly Day)> _dirtyDays = [];
    private readonly Dictionary<string, long> _revisions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var names = await UserNamesAsync(ct).ConfigureAwait(false);
        await LockedAsync(async () =>
        {
            foreach (var open in await repository.GetOpenInstancesAsync(ct).ConfigureAwait(false))
            {
                await repository.EndInstanceAsync(open.InstanceId, open.LastSeenUtc, EndReasons.ServiceStoppedUnexpectedly, ct).ConfigureAwait(false);
                _history.Ended(
                    open.AccountSid, names.GetValueOrDefault(open.AccountSid, open.AccountSid), open.DisplayName, open.ProcessName,
                    open.ProgramPath, open.InstanceId, open.LastSeenUtc - open.StartedUtc, EndReasons.ServiceStoppedUnexpectedly);
            }

            await PurgeOldCoreAsync(ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ApplyAsync(TrackerOutput output, IReadOnlyDictionary<string, string> userNames, bool isTick, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(userNames);
        return LockedAsync(async () =>
        {
            foreach (var instance in output.Instances)
            {
                var userName = userNames.GetValueOrDefault(instance.AccountSid, instance.AccountSid);
                await (instance is InstanceEnded ended ? EndAsync(ended, userName, ct) : StartAsync(instance, userName, ct)).ConfigureAwait(false);
            }

            foreach (var credit in output.Credits)
            {
                if (_appIds.TryGetValue(AppKey(credit.AccountSid, credit.ProgramPath), out var appId))
                {
                    AddPending(appId, credit.Day, credit.Seconds);
                    _dirtyDays.Add((credit.AccountSid, credit.Day));
                }
            }

            foreach (var seen in output.Seen)
            {
                if (_instanceIds.TryGetValue(seen.InstanceKey, out var instanceId))
                {
                    _pendingSeen[instanceId] = seen.LastSeenUtc;
                }
            }

            if (isTick)
            {
                await PersistAndBroadcastAsync(ct).ConfigureAwait(false);
                _history.Flush();
            }
        }, ct);
    }

    /// <inheritdoc />
    public async Task<AccountUsageDto> GetAccountUsageAsync(string accountSid, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);

        // Checked before taking the lock: the account service may call PurgeMissingAccountsAsync under its own lock.
        var inventory = await userAccounts.GetSnapshotAsync(ct).ConfigureAwait(false);
        if (!inventory.Accounts.Any(a => StringComparer.OrdinalIgnoreCase.Equals(a.Sid, accountSid)))
        {
            throw new UnknownAccountException();
        }

        return await LockedAsync(async () =>
        {
            var today = Today();
            var rows = await repository.GetUsageAsync(accountSid, today.AddDays(1 - RetentionDays), today, ct).ConfigureAwait(false);
            var revision = _revisions.GetValueOrDefault(accountSid, 1);
            var days = rows.GroupBy(r => r.Day).ToDictionary(g => g.Key, g => Apps(g));
            days.TryAdd(today, []);
            return new AccountUsageDto(
                accountSid,
                today,
                [.. days.OrderByDescending(d => d.Key).Select(d => new DayUsageDto(revision, null, accountSid, d.Key, today, d.Value))]);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task PurgeOldDataAsync(CancellationToken ct = default) => LockedAsync(() => PurgeOldCoreAsync(ct), ct);

    /// <inheritdoc />
    public Task PublishTodayAsync(IReadOnlyCollection<string> accountSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accountSids);
        return LockedAsync(async () =>
        {
            var today = Today();
            foreach (var sid in accountSids)
            {
                await PublishDayAsync(sid, today, today, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    /// <inheritdoc />
    public Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return LockedAsync(async () =>
        {
            foreach (var purge in await repository.PurgeAccountsNotInAsync(existingSids, ct).ConfigureAwait(false))
            {
                logger.LogInformation(
                    "Purged all recorded data of deleted account {AccountSid}: {Apps} apps, {Instances} history entries, {DayRows} daily entries.",
                    purge.AccountSid, purge.Apps, purge.Instances, purge.DayRows);
                ForgetAccount(purge.AccountSid);
            }
        }, ct);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task StartAsync(InstanceEvent started, string userName, CancellationToken ct)
    {
        var record = await repository.GetOrCreateAppAsync(
            started.AccountSid, started.ProgramPath, started.ProcessName, started.DisplayName, started.StartedUtc, ct).ConfigureAwait(false);
        if (record.Created)
        {
            _history.NewApp(userName, started.DisplayName, started.ProcessName, started.ProgramPath);
        }

        _appIds[AppKey(started.AccountSid, started.ProgramPath)] = record.AppId;
        var instanceId = await repository.StartInstanceAsync(record.AppId, started.StartedUtc, ct).ConfigureAwait(false);
        _instanceIds[started.InstanceKey] = instanceId;
        _history.Started(started.AccountSid, userName, started.DisplayName, started.ProcessName, started.ProgramPath, instanceId);

        // A new app appears in the report at once with 00:00 (ADR-012 §5).
        var day = LocalDay(started.StartedUtc);
        AddPending(record.AppId, day, 0);
        _dirtyDays.Add((started.AccountSid, day));
    }

    private async Task EndAsync(InstanceEnded ended, string userName, CancellationToken ct)
    {
        if (!_instanceIds.Remove(ended.InstanceKey, out var instanceId))
        {
            return;
        }

        _pendingSeen.Remove(instanceId);
        await repository.EndInstanceAsync(instanceId, ended.EndedUtc, ended.Reason, ct).ConfigureAwait(false);
        _history.Ended(
            ended.AccountSid, userName, ended.DisplayName, ended.ProcessName, ended.ProgramPath, instanceId,
            ended.EndedUtc - ended.StartedUtc, ended.Reason);
    }

    private async Task PersistAndBroadcastAsync(CancellationToken ct)
    {
        try
        {
            await repository.ApplyAsync(
                [.. _pendingSeconds.Select(p => new UsageIncrement(p.Key.AppId, p.Key.Day, p.Value))],
                [.. _pendingSeen.Select(p => new InstanceSeenRecord(p.Key, p.Value))],
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Persistence boundary: keep the data in memory and retry with the next tick.
            logger.LogWarning(ex, "Persisting the usage failed; it is kept and retried with the next tick.");
            return;
        }

        _pendingSeconds.Clear();
        _pendingSeen.Clear();
        var today = Today();
        foreach (var (sid, day) in _dirtyDays.ToList())
        {
            await PublishDayAsync(sid, day, today, ct).ConfigureAwait(false);
        }

        _dirtyDays.Clear();
    }

    private async Task PublishDayAsync(string sid, DateOnly day, DateOnly today, CancellationToken ct)
    {
        var apps = Apps(await repository.GetUsageAsync(sid, day, day, ct).ConfigureAwait(false));
        var revision = _revisions.GetValueOrDefault(sid, 1) + 1;
        _revisions[sid] = revision;
        try
        {
            await broadcaster.BroadcastAsync(new DayUsageDto(revision, null, sid, day, today, apps)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ADR-010 §9: a failed broadcast never affects recording; clients recover with their next fetch.
            logger.LogWarning(ex, "Broadcasting the usage of {AccountSid} for {Day} failed.", sid, day);
        }
    }

    private async Task PurgeOldCoreAsync(CancellationToken ct)
    {
        var cutoffDay = Today().AddDays(1 - RetentionDays);
        var (dayRows, instances) = await repository.PurgeOlderThanAsync(
            cutoffDay, timeProvider.GetUtcNow().AddDays(-RetentionDays), ct).ConfigureAwait(false);
        if (dayRows + instances > 0)
        {
            logger.LogInformation(
                "Purged usage data older than {CutoffDay}: {DayRows} daily entries, {Instances} history entries.",
                cutoffDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), dayRows, instances);
        }
    }

    private void ForgetAccount(string sid)
    {
        var prefix = sid.ToUpperInvariant() + "|";
        var appIds = _appIds.Where(a => a.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var (key, appId) in appIds)
        {
            _appIds.Remove(key);
            foreach (var pending in _pendingSeconds.Keys.Where(k => k.AppId == appId).ToList())
            {
                _pendingSeconds.Remove(pending);
            }
        }

        _dirtyDays.RemoveWhere(d => StringComparer.OrdinalIgnoreCase.Equals(d.Sid, sid));
    }

    private async Task<Dictionary<string, string>> UserNamesAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await userAccounts.GetSnapshotAsync(ct).ConfigureAwait(false);
            return snapshot.Accounts.ToDictionary(a => a.Sid, a => a.UserName, StringComparer.OrdinalIgnoreCase);
        }
        catch (AccountInventoryUnavailableException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void AddPending(long appId, DateOnly day, long seconds)
    {
        _pendingSeconds[(appId, day)] = _pendingSeconds.GetValueOrDefault((appId, day)) + seconds;
    }

    private static List<AppUsageDto> Apps(IEnumerable<DayAppUsage> rows) => [.. rows.Select(r => new AppUsageDto(r.AppId, r.DisplayName, r.Seconds))];

    private DateOnly Today() => LocalDay(timeProvider.GetUtcNow());

    private DateOnly LocalDay(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeProvider.LocalTimeZone).DateTime);

    private async Task LockedAsync(Func<Task> work, CancellationToken ct)
    {
        await LockedAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    private async Task<T> LockedAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string AppKey(string sid, string path) => $"{sid.ToUpperInvariant()}|{path.ToUpperInvariant()}";
}
