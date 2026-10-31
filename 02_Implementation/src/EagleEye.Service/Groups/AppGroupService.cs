using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Groups;

/// <summary>
/// State owner of "AppGroups" and "AppGroupRules:{sid}" (US-008 Decision 4). One lock serializes all writes; inside it:
/// validate → store → replace the snapshot → revision++ → log → broadcast → ack. Create and delete also broadcast the group
/// rules of every standard account (a row per group). The app catalog follows the app-record observer and the controlled set
/// of the accounting loop; it is broadcast when the listed apps change (AC-4). Group rules of deleted accounts are purged;
/// groups and members stay (AC-39, AC-40).
/// </summary>
public sealed class AppGroupService(
    IAppGroupRepository repository,
    IAccountRulesBroadcaster broadcaster,
    IUserAccountService userAccounts,
    EnforcementIgnoreList ignoreList,
    TimeProvider timeProvider,
    ILogger<AppGroupService> logger) : IAppGroupService, IAppRecordObserver, IAccountDataPurger, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AppGroupCatalog _catalog = new();
    private readonly Dictionary<string, Guid> _lastRuleRequests = new(StringComparer.OrdinalIgnoreCase);
    private AppGroupsSnapshot _snapshot = AppGroupsSnapshot.Empty;
    private Guid? _lastGroupsRequest;
    private long _revision = 1;

    /// <inheritdoc />
    public AppGroupsSnapshot Current => Volatile.Read(ref _snapshot);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        var stored = await repository.LoadAllAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _snapshot, AppGroupsSnapshot.From(stored));
        foreach (var app in stored.Catalog.Where(a => !ignoreList.IsIgnored(a.ProgramPath)))
        {
            _catalog.See(app);
        }

        logger.LogInformation(
            "App groups loaded: {Groups} groups with {Members} apps, {Rules} group rules.", stored.Groups.Count, stored.Members.Count, stored.Rules.Count);
        return true;
    }, ct);

    /// <inheritdoc />
    public Task<AppGroupsDto> GetAsync(CancellationToken ct = default) => LockedAsync(() => Task.FromResult(GroupsDto()), ct);

    /// <inheritdoc />
    public Task<StateWriteAckDto> CreateAsync(Guid requestId, string name, string deviceName, CancellationToken ct = default)
    {
        var normalized = ValidName(name);
        return WriteGroupsAsync(requestId, deviceName, rulesOfAllAccounts: true, async snapshot =>
        {
            EnsureUnique(snapshot, normalized, exceptGroupId: null);
            var id = await repository.CreateGroupAsync(normalized, AppGroupNames.Key(normalized), timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            return (snapshot.WithGroup(id, normalized), AppGroupChangeLog.Created(normalized));
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> RenameAsync(Guid requestId, long groupId, string name, string deviceName, CancellationToken ct = default)
    {
        var normalized = ValidName(name);
        return WriteGroupsAsync(requestId, deviceName, rulesOfAllAccounts: false, async snapshot =>
        {
            var old = snapshot.Names.GetValueOrDefault(groupId) ?? throw new GroupNotFoundException();
            EnsureUnique(snapshot, normalized, groupId);
            if (!await repository.RenameGroupAsync(groupId, normalized, AppGroupNames.Key(normalized), timeProvider.GetUtcNow(), ct).ConfigureAwait(false))
            {
                throw new GroupNotFoundException();
            }

            return (snapshot.WithGroup(groupId, normalized), AppGroupChangeLog.Renamed(old, normalized));
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> DeleteAsync(Guid requestId, long groupId, string deviceName, CancellationToken ct = default) =>
        WriteGroupsAsync(requestId, deviceName, rulesOfAllAccounts: true, async snapshot =>
        {
            var name = snapshot.Names.GetValueOrDefault(groupId) ?? throw new GroupNotFoundException();
            if (!await repository.DeleteGroupAsync(groupId, ct).ConfigureAwait(false))
            {
                throw new GroupNotFoundException();
            }

            return (snapshot.WithoutGroup(groupId), AppGroupChangeLog.Deleted(name));
        }, ct);

    /// <inheritdoc />
    public Task<StateWriteAckDto> MoveAsync(Guid requestId, string programPath, long? groupId, string deviceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        return WriteGroupsAsync(requestId, deviceName, rulesOfAllAccounts: false, async snapshot =>
        {
            var app = _catalog.Listed.GetValueOrDefault(programPath) ?? throw new AppNotFoundException();
            var groupName = groupId is { } id ? snapshot.Names.GetValueOrDefault(id) ?? throw new GroupNotFoundException() : null;
            await repository.SetMemberAsync(app.ProgramPath, groupId, timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            return (snapshot.WithMember(app.ProgramPath, groupId), AppGroupChangeLog.Moved(app.DisplayName, groupName));
        }, ct);
    }

    /// <inheritdoc />
    public async Task<AppGroupRulesDto> GetRulesAsync(string accountSid, CancellationToken ct = default)
    {
        var account = await FindAccountAsync(accountSid, ct).ConfigureAwait(false);
        return await LockedAsync(() => Task.FromResult(RulesDto(account.Sid)), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBudgetAsync(
        Guid requestId, string accountSid, long groupId, AppBudgetPart part, int value, string deviceName, CancellationToken ct = default)
    {
        if (!AppRuleRules.IsValidPart(part, value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid budget part.");
        }

        return WriteRuleAsync(requestId, accountSid, groupId, deviceName, r => r with { BudgetMinutes = AppRuleRules.Combine(r.BudgetMinutes, part, value) }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetDayAsync(
        Guid requestId, string accountSid, long groupId, DayOfWeek day, bool isAllowed, string deviceName, CancellationToken ct = default)
    {
        var flag = BreakTimeRules.ToDays(day);
        return WriteRuleAsync(requestId, accountSid, groupId, deviceName, r => r with { Days = isAllowed ? r.Days | flag : r.Days & ~flag }, ct);
    }

    /// <inheritdoc />
    public Task OnControlledAccountsAsync(IReadOnlyCollection<string> accountSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accountSids);
        return LockedAsync(() => BroadcastCatalogIfAsync(_catalog.SetControlled(accountSids)), ct);
    }

    /// <inheritdoc />
    public Task OnAppRecordAsync(string accountSid, long appId, string programPath, string displayName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(programPath);
        ArgumentNullException.ThrowIfNull(displayName);
        return ignoreList.IsIgnored(programPath)
            ? Task.CompletedTask
            : LockedAsync(() => BroadcastCatalogIfAsync(_catalog.See(new StoredCatalogApp(accountSid, programPath, displayName, timeProvider.GetUtcNow()))), ct);
    }

    /// <inheritdoc />
    public Task PurgeMissingAccountsAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);
        return LockedAsync(async () =>
        {
            var purged = await repository.PurgeRulesNotInAsync(existingSids, ct).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, _snapshot.WithoutRulesOf(purged));
            foreach (var sid in purged)
            {
                _lastRuleRequests.Remove(sid);
                logger.LogInformation("Purged the group rules of deleted account {AccountSid}.", sid);
            }

            return await BroadcastCatalogIfAsync(_catalog.RemoveAccountsNotIn(existingSids)).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<StateWriteAckDto> WriteGroupsAsync(
        Guid requestId, string deviceName, bool rulesOfAllAccounts, Func<AppGroupsSnapshot, Task<(AppGroupsSnapshot Snapshot, string Change)>> apply,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(deviceName);

        // Read before taking the lock: the account service calls PurgeMissingAccountsAsync under its own lock.
        IReadOnlyList<UserAccountDto> accounts = rulesOfAllAccounts ? (await userAccounts.GetSnapshotAsync(ct).ConfigureAwait(false)).Accounts : [];
        return await LockedAsync(async () =>
        {
            var (snapshot, change) = await apply(_snapshot).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, snapshot);
            _revision++;
            _lastGroupsRequest = requestId;
            logger.LogInformation(
                "App groups changed by {DeviceName}: {Change} (request {RequestId}, revision {Revision}).", deviceName, change, requestId, _revision);
            await BroadcastAsync(() => broadcaster.BroadcastAsync(GroupsDto()), "AppGroups").ConfigureAwait(false);
            foreach (var account in accounts)
            {
                await BroadcastAsync(() => broadcaster.BroadcastAsync(RulesDto(account.Sid)), account.Sid).ConfigureAwait(false);
            }

            return new StateWriteAckDto(_revision);
        }, ct).ConfigureAwait(false);
    }

    private async Task<StateWriteAckDto> WriteRuleAsync(
        Guid requestId, string accountSid, long groupId, string deviceName, Func<LimitRule, LimitRule> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(deviceName);
        var account = await FindAccountAsync(accountSid, ct).ConfigureAwait(false);
        return await LockedAsync(async () =>
        {
            var name = _snapshot.Names.GetValueOrDefault(groupId) ?? throw new GroupNotFoundException();
            var rule = change(_snapshot.RuleFor(groupId, account.Sid));
            await repository.SetRuleAsync(groupId, account.Sid, rule.BudgetMinutes, rule.Days, timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, _snapshot.WithRule(groupId, account.Sid, rule));
            _revision++;
            _lastRuleRequests[account.Sid] = requestId;
            logger.LogInformation(
                "Group rules of account {UserName} ({AccountSid}) changed by {DeviceName}: {Change} (request {RequestId}, revision {Revision}).",
                account.UserName, account.Sid, deviceName, AppGroupChangeLog.RuleChanged(name, rule), requestId, _revision);
            await BroadcastAsync(() => broadcaster.BroadcastAsync(RulesDto(account.Sid)), account.Sid).ConfigureAwait(false);
            return new StateWriteAckDto(_revision);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>A catalog change is broadcast without a log entry (like a new app record on Rules, US-006).</summary>
    private async Task<bool> BroadcastCatalogIfAsync(bool changed)
    {
        if (changed)
        {
            _revision++;
            await BroadcastAsync(() => broadcaster.BroadcastAsync(GroupsDto()), "AppGroups").ConfigureAwait(false);
        }

        return changed;
    }

    private AppGroupsDto GroupsDto() => new(
        _revision, _lastGroupsRequest,
        [.. _snapshot.Names.Select(n => new AppGroupDto(n.Key, n.Value))],
        [.. _catalog.Listed.Values.Select(a => new GroupableAppDto(a.ProgramPath, a.DisplayName, _snapshot.GroupOf(a.ProgramPath)))]);

    private AppGroupRulesDto RulesDto(string sid)
    {
        Guid? lastRequest = _lastRuleRequests.TryGetValue(sid, out var id) ? id : null;
        return new AppGroupRulesDto(
            _revision, lastRequest, sid,
            [.. _snapshot.Names.Keys.Select(groupId => (Id: groupId, Rule: _snapshot.RuleFor(groupId, sid)))
                .Select(r => new AppGroupRuleDto(r.Id, r.Rule.BudgetMinutes, r.Rule.Days))]);
    }

    private async Task BroadcastAsync(Func<Task> send, string area)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ADR-010 §9: the state is stored; clients that missed the broadcast recover on their next fetch.
            logger.LogWarning(ex, "Broadcasting the app groups ({Area}, revision {Revision}) failed.", area, _revision);
        }
    }

    private static string ValidName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return AppGroupNames.IsValid(name) ? AppGroupNames.Normalize(name) : throw new ArgumentException("The group name is not valid.", nameof(name));
    }

    private static void EnsureUnique(AppGroupsSnapshot snapshot, string name, long? exceptGroupId)
    {
        var key = AppGroupNames.Key(name);
        if (snapshot.Names.Any(n => n.Key != exceptGroupId && AppGroupNames.Key(n.Value) == key))
        {
            throw new DuplicateGroupNameException();
        }
    }

    private async Task<UserAccountDto> FindAccountAsync(string accountSid, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        var inventory = await userAccounts.GetSnapshotAsync(ct).ConfigureAwait(false);
        return inventory.Accounts.FirstOrDefault(a => StringComparer.OrdinalIgnoreCase.Equals(a.Sid, accountSid))
            ?? throw new UnknownAccountException();
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
