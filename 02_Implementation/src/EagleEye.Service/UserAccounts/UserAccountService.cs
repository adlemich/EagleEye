using EagleEye.Service.Data;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.UserAccounts;

/// <summary>
/// State owner of the state area "UserAccounts" (ADR-010 §2, §9; US-003). Holds the current
/// inventory, the stored selections and the revision behind one lock, so writes and inventory
/// changes are serialized and broadcasts leave in revision order. The selection of an account is
/// the stored value for its SID, default <c>false</c> (AC-18). Accounts that are admins are not in
/// the snapshot, but their stored value is kept (AC-20, AC-21); rows of deleted SIDs are deleted
/// (AC-22). A failed or empty read of the Windows accounts never changes or deletes anything.
/// </summary>
public sealed class UserAccountService(
    ILocalAccountSource accountSource,
    IAccountSelectionRepository repository,
    IUserAccountsBroadcaster broadcaster,
    TimeProvider timeProvider,
    Lazy<IEnumerable<IAccountDataPurger>> dataPurgers,
    ILogger<UserAccountService> logger) : IUserAccountService, IDisposable
{
    private const string ReadFailedMessage = "Reading the local accounts failed; the inventory is kept unchanged.";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, bool> _selections = new(StringComparer.OrdinalIgnoreCase);
    private AccountInventory? _inventory;
    private long _revision = 1;
    private Guid? _lastChangeRequestId;

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        _selections = new Dictionary<string, bool>(await repository.LoadAllAsync(ct).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase);
        var inventory = ReadInventory(LogLevel.Error);
        if (inventory is null)
        {
            return;
        }

        await ForgetDeletedAsync(inventory, ct).ConfigureAwait(false);
        _inventory = inventory;
        logger.LogInformation(
            "Account inventory loaded: {Count} standard accounts, {Selected} under parental control.",
            inventory.StandardAccounts.Count,
            inventory.StandardAccounts.Keys.Count(sid => _selections.GetValueOrDefault(sid)));
    }, ct);

    /// <inheritdoc />
    public Task<UserAccountListDto> GetSnapshotAsync(CancellationToken ct = default) => LockedAsync(() =>
    {
        return Task.FromResult(BuildSnapshot(CurrentInventory()));
    }, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<ControlledAccount>> GetControlledAccountsAsync(CancellationToken ct = default) => LockedAsync(() =>
    {
        IReadOnlyList<ControlledAccount> controlled = _inventory is null
            ? []
            : [.. _inventory.StandardAccounts.Values
                .Where(a => _selections.GetValueOrDefault(a.Sid))
                .OrderBy(a => a.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(a => new ControlledAccount(a.Sid, a.UserName))];
        return Task.FromResult(controlled);
    }, ct);

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetParentalControlAsync(
        Guid requestId, string sid, bool isUnderParentalControl, string deviceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        ArgumentNullException.ThrowIfNull(deviceName);

        return LockedAsync(async () =>
        {
            var inventory = CurrentInventory();
            if (!inventory.StandardAccounts.TryGetValue(sid, out var account))
            {
                logger.LogWarning("Setting parental control for {AccountSid} rejected: unknown account.", sid);
                throw new UnknownAccountException();
            }

            await repository.SetAsync(account.Sid, account.UserName, isUnderParentalControl, timeProvider.GetUtcNow(), ct)
                .ConfigureAwait(false);
            _selections[account.Sid] = isUnderParentalControl;
            _revision++;
            _lastChangeRequestId = requestId;
            logger.LogInformation(
                "Account {UserName} ({AccountSid}): under parental control = {UnderParentalControl} (set by parent device {DeviceName}, request {RequestId}, revision {Revision}).",
                account.UserName, account.Sid, isUnderParentalControl ? "yes" : "no", deviceName, requestId, _revision);
            await BroadcastAsync(BuildSnapshot(inventory)).ConfigureAwait(false);
            return new StateWriteAckDto(_revision);
        }, ct);
    }

    /// <inheritdoc />
    public Task RefreshInventoryAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        var inventory = ReadInventory(LogLevel.Warning);
        if (inventory is null || (_inventory is not null && _inventory.HasSameContent(inventory)))
        {
            return;
        }

        await ForgetDeletedAsync(inventory, ct).ConfigureAwait(false);
        var previous = _inventory;
        _inventory = inventory;
        if (previous is not null && previous.HasSameStandardAccounts(inventory))
        {
            // Only admin accounts were added or deleted: nothing the apps show has changed.
            return;
        }

        _revision++;
        _lastChangeRequestId = null;
        var (added, removed, changed) = inventory.Compare(previous);
        logger.LogInformation(
            "Account inventory changed (revision {Revision}): added [{Added}], removed [{Removed}], changed [{Changed}]; {Count} standard accounts.",
            _revision, string.Join(", ", added), string.Join(", ", removed), string.Join(", ", changed), inventory.StandardAccounts.Count);
        await BroadcastAsync(BuildSnapshot(inventory)).ConfigureAwait(false);
    }, ct);

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>Reads the accounts; <c>null</c> if the read failed or Windows returned no account.</summary>
    private AccountInventory? ReadInventory(LogLevel failureLevel)
    {
        IReadOnlyList<LocalAccountInfo> accounts;
        try
        {
            accounts = accountSource.GetAccounts();
        }
        catch (Exception ex)
        {
            // Win32 boundary: whatever went wrong, a failed read must never wipe selections.
            logger.Log(failureLevel, ex, ReadFailedMessage);
            return null;
        }

        if (accounts.Count == 0)
        {
            logger.Log(failureLevel, "Windows returned no local accounts; the inventory is kept unchanged.");
            return null;
        }

        return AccountInventory.Create(accounts);
    }

    private async Task ForgetDeletedAsync(AccountInventory inventory, CancellationToken ct)
    {
        var existing = inventory.AllSids.ToList();

        // US-004 AC-9, US-005 AC-20/AC-36, FR-SVC-047: recorded usage, rules, history and findings of deleted accounts
        // go too. Only after a successful read.
        foreach (var purger in dataPurgers.Value)
        {
            await purger.PurgeMissingAccountsAsync(existing, ct).ConfigureAwait(false);
        }

        var forgotten = await repository.DeleteMissingAsync(existing, ct).ConfigureAwait(false);
        if (forgotten.Count == 0)
        {
            return;
        }

        foreach (var sid in forgotten)
        {
            _selections.Remove(sid);
        }

        logger.LogInformation(
            "Forgot the parental-control selection of {Count} deleted account(s): {Sids}.", forgotten.Count, string.Join(", ", forgotten));
    }

    private AccountInventory CurrentInventory() => _inventory ?? throw new AccountInventoryUnavailableException();

    private UserAccountListDto BuildSnapshot(AccountInventory inventory)
    {
        var accounts = inventory.StandardAccounts.Values
            .OrderBy(a => a.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(a => new UserAccountDto(a.Sid, a.UserName, a.FullName, a.IsDisabled, _selections.GetValueOrDefault(a.Sid)))
            .ToList();
        return new UserAccountListDto(_revision, _lastChangeRequestId, accounts);
    }

    private async Task BroadcastAsync(UserAccountListDto snapshot)
    {
        try
        {
            await broadcaster.BroadcastAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ADR-010 §9: the state is stored; clients that missed the broadcast recover on their next fetch.
            logger.LogWarning(ex, "Broadcasting the account inventory (revision {Revision}) failed.", snapshot.Revision);
        }
    }

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
}
