using EagleEye.Service.Data;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// State owner of the areas "AccountRules:{sid}" (ADR-010 §9, US-005 Decision 1). One lock serializes all writes;
/// inside it: validate against the stored entry → store → replace the snapshot → revision++ → log → broadcast → ack.
/// The revision is one in-memory counter for all accounts (strictly increasing per area). Rules of accounts that are
/// not controlled are kept and editable (AC-20); rules of deleted accounts are purged (FR-SVC-047).
/// </summary>
public sealed class BreakTimeService(
    IBreakTimeRepository repository,
    IAccountRulesBroadcaster broadcaster,
    IUserAccountService userAccounts,
    TimeProvider timeProvider,
    ILogger<BreakTimeService> logger) : IBreakTimeService, IAccountDataPurger, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Guid> _lastRequests = new(StringComparer.OrdinalIgnoreCase);
    private RulesSnapshot _snapshot = RulesSnapshot.Empty;
    private long _revision = 1;

    /// <inheritdoc />
    public RulesSnapshot Current => Volatile.Read(ref _snapshot);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        var stored = await repository.LoadAllAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _snapshot, RulesSnapshot.From(stored));
        logger.LogInformation(
            "Break times loaded: {Entries} entries ({Active} on) of {Accounts} accounts, {Texts} changed display texts.",
            stored.Entries.Count, stored.Entries.Count(e => e.Entry.IsActive),
            stored.Entries.Select(e => e.AccountSid).Distinct(StringComparer.OrdinalIgnoreCase).Count(), stored.Texts.Count);
        return true;
    }, ct);

    /// <inheritdoc />
    public async Task<AccountRulesDto> GetAsync(string accountSid, CancellationToken ct = default)
    {
        var account = await FindAccountAsync(accountSid, ct).ConfigureAwait(false);
        return await LockedAsync(() => Task.FromResult(BuildDto(account.Sid)), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> AddEntryAsync(Guid requestId, string accountSid, string deviceName, CancellationToken ct = default)
    {
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            if (rules.Entries.Count >= BreakTimeRules.MaxEntriesPerAccount)
            {
                throw new TooManyEntriesException();
            }

            var values = new BreakTimeEntryDto(0, false, BreakTimeRules.DefaultStartMinute, BreakTimeRules.DefaultEndMinute, BreakTimeRules.DefaultDays);
            var id = await repository.InsertEntryAsync(sid, values, timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            var entry = values with { EntryId = id };
            return (rules with { Entries = rules.Entries.Add(entry) }, BreakTimeChangeLog.Added(entry));
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> DeleteEntryAsync(Guid requestId, string accountSid, long entryId, string deviceName, CancellationToken ct = default)
    {
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            var entry = rules.Find(entryId) ?? throw new EntryNotFoundException();
            if (!await repository.DeleteEntryAsync(sid, entryId, ct).ConfigureAwait(false))
            {
                throw new EntryNotFoundException();
            }

            return (rules with { Entries = rules.Entries.Remove(entry) }, BreakTimeChangeLog.Deleted(entryId));
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetActiveAsync(
        Guid requestId, string accountSid, long entryId, bool isActive, string deviceName, CancellationToken ct = default)
    {
        return UpdateEntryAsync(requestId, accountSid, entryId, deviceName, entry => entry with { IsActive = isActive }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetTimeAsync(
        Guid requestId, string accountSid, long entryId, BreakTimeBoundary boundary, int minute, string deviceName, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, BreakTimeRules.LastMinute);
        return UpdateEntryAsync(requestId, accountSid, entryId, deviceName, entry =>
        {
            var changed = boundary == BreakTimeBoundary.Start ? entry with { StartMinute = minute } : entry with { EndMinute = minute };
            return BreakTimeRules.IsValidTimes(changed.StartMinute, changed.EndMinute)
                ? changed
                : throw new RuleValidationException(RuleViolation.EndNotAfterStart);
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long entryId, DayOfWeek day, bool isSelected, string deviceName, CancellationToken ct = default)
    {
        var flag = BreakTimeRules.ToDays(day);
        return UpdateEntryAsync(requestId, accountSid, entryId, deviceName, entry =>
        {
            var days = isSelected ? entry.Days | flag : entry.Days & ~flag;
            return days == BreakTimeDays.None ? throw new RuleValidationException(RuleViolation.NoDaySelected) : entry with { Days = days };
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetDisplayTextAsync(Guid requestId, string accountSid, string text, string deviceName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = BreakTimeRules.NormalizeLineBreaks(text);
        if (!BreakTimeRules.IsValidDisplayText(normalized))
        {
            throw new ArgumentException("The display text is not valid.", nameof(text));
        }

        var custom = string.IsNullOrWhiteSpace(normalized) || normalized == BreakTimeRules.DefaultDisplayText ? null : normalized;
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            await repository.SetDisplayTextAsync(sid, custom, timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            return (rules with { CustomText = custom }, custom is null ? BreakTimeChangeLog.TextReset() : BreakTimeChangeLog.TextChanged());
        }, ct);
    }

    /// <inheritdoc />
    public Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return LockedAsync(async () =>
        {
            var purged = await repository.PurgeAccountsNotInAsync(existingSids, ct).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, _snapshot.Without(purged.Select(p => p.AccountSid)));
            foreach (var account in purged)
            {
                _lastRequests.Remove(account.AccountSid);
                logger.LogInformation(
                    "Purged the break times of deleted account {AccountSid}: {Entries} entries, display text {Text}.",
                    account.AccountSid, account.Entries, account.HadText ? "deleted" : "not changed");
            }

            return true;
        }, ct);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private Task<StateWriteAckDto> UpdateEntryAsync(
        Guid requestId, string accountSid, long entryId, string deviceName, Func<BreakTimeEntryDto, BreakTimeEntryDto> change, CancellationToken ct)
    {
        return WriteAsync(requestId, accountSid, deviceName, async (sid, rules) =>
        {
            var entry = rules.Find(entryId) ?? throw new EntryNotFoundException();
            var changed = change(entry);
            if (!await repository.UpdateEntryAsync(sid, changed, timeProvider.GetUtcNow(), ct).ConfigureAwait(false))
            {
                throw new EntryNotFoundException();
            }

            return (rules with { Entries = rules.Entries.Replace(entry, changed) }, BreakTimeChangeLog.Changed(changed));
        }, ct);
    }

    private async Task<StateWriteAckDto> WriteAsync(
        Guid requestId, string accountSid, string deviceName, Func<string, AccountRules, Task<(AccountRules Rules, string Change)>> apply, CancellationToken ct)
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
                "Break times of account {UserName} ({AccountSid}) changed by {DeviceName}: {Change} (request {RequestId}, revision {Revision}).",
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

    private AccountRulesDto BuildDto(string sid)
    {
        var rules = _snapshot.For(sid);
        Guid? lastRequest = _lastRequests.TryGetValue(sid, out var id) ? id : null;
        return new AccountRulesDto(_revision, lastRequest, sid, rules.Entries, rules.DisplayText, rules.IsDefaultText);
    }

    private async Task BroadcastAsync(AccountRulesDto snapshot)
    {
        try
        {
            await broadcaster.BroadcastAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ADR-010 §9: the state is stored; clients that missed the broadcast recover on their next fetch.
            logger.LogWarning(ex, "Broadcasting the rules of {AccountSid} (revision {Revision}) failed.", snapshot.AccountSid, snapshot.Revision);
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
