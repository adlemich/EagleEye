using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.Shared.Resources;

namespace EagleEye.Service.Rules;

/// <summary>
/// State owner of the areas "AppRules:{sid}" (ADR-010 §9, US-006 Decision 1). One lock serializes all writes; inside it:
/// validate against the stored rule → store → replace the snapshot → revision++ → log → broadcast → ack. The revision is
/// one in-memory counter for all accounts. New and renamed app records (from <c>UsageService</c>) are broadcast without a
/// log entry. Programs of the enforcement ignore list are never listed (AC-3). Rules of deleted accounts go with their
/// app records (cascade); their budget texts are purged here (FR-SVC-047).
/// </summary>
public sealed class AppRuleService(
    IAppRuleRepository repository,
    IAccountRulesBroadcaster broadcaster,
    IUserAccountService userAccounts,
    EnforcementIgnoreList ignoreList,
    TimeProvider timeProvider,
    ILogger<AppRuleService> logger) : IAppRuleService, IAppRecordObserver, IAccountDataPurger, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Guid> _lastRequests = new(StringComparer.OrdinalIgnoreCase);
    private AppRulesSnapshot _snapshot = AppRulesSnapshot.Empty;
    private long _revision = 1;

    /// <inheritdoc />
    public AppRulesSnapshot Current => Volatile.Read(ref _snapshot);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        var stored = await repository.LoadAllAsync(ct).ConfigureAwait(false);
        var apps = stored.Apps.Where(a => !ignoreList.IsIgnored(a.ProgramPath)).ToList();
        Volatile.Write(ref _snapshot, AppRulesSnapshot.From(stored, ignoreList.IsIgnored));
        logger.LogInformation(
            "App rules loaded: {Rules} rules of {Apps} apps of {Accounts} accounts, {Texts} changed budget texts.",
            apps.Count(a => a.BudgetMinutes != AppRuleRules.DefaultBudgetMinutes || a.Days != AppRuleRules.DefaultDays), apps.Count,
            apps.Select(a => a.AccountSid).Distinct(StringComparer.OrdinalIgnoreCase).Count(), stored.BudgetTexts.Count);
        return true;
    }, ct);

    /// <inheritdoc />
    public async Task<AppRulesDto> GetAsync(string accountSid, CancellationToken ct = default)
    {
        var account = await FindAccountAsync(accountSid, ct).ConfigureAwait(false);
        return await LockedAsync(() => Task.FromResult(BuildDto(account.Sid)), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBudgetAsync(
        Guid requestId, string accountSid, long appId, AppBudgetPart part, int value, string deviceName, CancellationToken ct = default)
    {
        if (!AppRuleRules.IsValidPart(part, value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid budget part.");
        }

        return UpdateRuleAsync(requestId, accountSid, appId, deviceName, app => app with { BudgetMinutes = AppRuleRules.Combine(app.BudgetMinutes, part, value) }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long appId, DayOfWeek day, bool isAllowed, string deviceName, CancellationToken ct = default)
    {
        var flag = BreakTimeRules.ToDays(day);
        return UpdateRuleAsync(requestId, accountSid, appId, deviceName, app => app with { Days = isAllowed ? app.Days | flag : app.Days & ~flag }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBudgetTextAsync(Guid requestId, string accountSid, string text, string deviceName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = BreakTimeRules.NormalizeLineBreaks(text);
        if (!BreakTimeRules.IsValidDisplayText(normalized))
        {
            throw new ArgumentException("The budget text is not valid.", nameof(text));
        }

        var custom = DefaultTexts.MeansDefault(DefaultTextKind.BudgetText, normalized) ? null : normalized;
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            await repository.SetBudgetTextAsync(sid, custom, timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            return (rules.WithBudgetText(custom), custom is null ? AppRuleChangeLog.TextReset() : AppRuleChangeLog.TextChanged());
        }, ct);
    }

    /// <inheritdoc />
    public Task OnAppRecordAsync(string accountSid, long appId, string programPath, string displayName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        ArgumentNullException.ThrowIfNull(displayName);
        if (ignoreList.IsIgnored(programPath))
        {
            return Task.CompletedTask;
        }

        return LockedAsync(async () =>
        {
            var rules = _snapshot.For(accountSid);
            var known = rules.Apps.GetValueOrDefault(appId);
            if (known is not null && known.DisplayName == displayName && known.ProgramPath == programPath)
            {
                return true;
            }

            var app = known is null
                ? new AppRuleEntry(appId, programPath, displayName, AppRuleRules.DefaultBudgetMinutes, AppRuleRules.DefaultDays)
                : known with { ProgramPath = programPath, DisplayName = displayName };
            Volatile.Write(ref _snapshot, _snapshot.With(accountSid, rules.WithApp(app)));
            _revision++;
            await BroadcastAsync(BuildDto(accountSid)).ConfigureAwait(false);
            return true;
        }, ct);
    }

    /// <inheritdoc />
    public Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return LockedAsync(async () =>
        {
            var texts = await repository.PurgeBudgetTextsNotInAsync(existingSids, ct).ConfigureAwait(false);
            var existing = new HashSet<string>(existingSids, StringComparer.OrdinalIgnoreCase);
            var gone = _snapshot.AccountSids.Where(sid => !existing.Contains(sid)).ToList();
            Volatile.Write(ref _snapshot, _snapshot.Without(gone));
            foreach (var sid in gone)
            {
                _lastRequests.Remove(sid);
            }

            foreach (var sid in texts)
            {
                logger.LogInformation("Purged the budget text of deleted account {AccountSid}.", sid);
            }

            return true;
        }, ct);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private Task<StateWriteAckDto> UpdateRuleAsync(
        Guid requestId, string accountSid, long appId, string deviceName, Func<AppRuleEntry, AppRuleEntry> change, CancellationToken ct)
    {
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            var app = rules.Apps.GetValueOrDefault(appId) ?? throw new AppNotFoundException();
            var changed = change(app);
            if (!await repository.SetRuleAsync(sid, appId, changed.BudgetMinutes, changed.Days, timeProvider.GetUtcNow(), ct).ConfigureAwait(false))
            {
                throw new AppNotFoundException();
            }

            return (rules.WithApp(changed), AppRuleChangeLog.RuleChanged(changed));
        }, ct);
    }

    private async Task<StateWriteAckDto> WriteAsync(
        Guid requestId, string accountSid, string deviceName, Func<string, AccountAppRules, Task<(AccountAppRules Rules, string Change)>> apply,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(deviceName);

        // Checked before taking the lock: the account service calls PurgeMissingAccountsAsync under its own lock.
        var account = await FindAccountAsync(accountSid, ct).ConfigureAwait(false);
        return await LockedAsync(async () =>
        {
            var (rules, change) = await apply(account.Sid, _snapshot.For(account.Sid)).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, _snapshot.With(account.Sid, rules));
            _revision++;
            _lastRequests[account.Sid] = requestId;
            logger.LogInformation(
                "App rules of account {UserName} ({AccountSid}) changed by {DeviceName}: {Change} (request {RequestId}, revision {Revision}).",
                account.UserName, account.Sid, deviceName, change, requestId, _revision);
            await BroadcastAsync(BuildDto(account.Sid)).ConfigureAwait(false);
            return new StateWriteAckDto(_revision);
        }, ct).ConfigureAwait(false);
    }

    private async Task<UserAccountDto> FindAccountAsync(string accountSid, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        var inventory = await userAccounts.GetSnapshotAsync(ct).ConfigureAwait(false);
        return inventory.Accounts.FirstOrDefault(a => StringComparer.OrdinalIgnoreCase.Equals(a.Sid, accountSid))
            ?? throw new UnknownAccountException();
    }

    private AppRulesDto BuildDto(string sid)
    {
        var rules = _snapshot.For(sid);
        Guid? lastRequest = _lastRequests.TryGetValue(sid, out var id) ? id : null;
        return new AppRulesDto(
            _revision, lastRequest, sid,
            [.. rules.Apps.Values.OrderBy(a => a.AppId).Select(a => new AppRuleDto(a.AppId, a.DisplayName, a.ProgramPath, a.BudgetMinutes, a.Days))],
            rules.BudgetText);
    }

    private async Task BroadcastAsync(AppRulesDto snapshot)
    {
        try
        {
            await broadcaster.BroadcastAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ADR-010 §9: the state is stored; clients that missed the broadcast recover on their next fetch.
            logger.LogWarning(ex, "Broadcasting the app rules of {AccountSid} (revision {Revision}) failed.", snapshot.AccountSid, snapshot.Revision);
        }
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
}
