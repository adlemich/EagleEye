using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of the state area "AccountRules:{sid}" (ADR-010, coding guidelines §7.5) for the selected account. On
/// every confirmed (re)connect and every account change it resets, shows "Loading" and fetches <c>GetAccountRules</c>;
/// results of an earlier selection or connection are ignored (generation counter). Broadcasts of the selected account
/// are applied with the revision rule. Writes follow <see cref="UserAccountsModel"/>: a write is confirmed by a
/// snapshot with its request id or with a revision at least the acknowledged one; timeout <see cref="WriteTimeout"/>
/// (AC-18: the app reverts before 5 s); unknown outcomes lead to a new fetch. Disconnect → no data (AC-6). Never polls.
/// </summary>
public sealed class AccountRulesModel : IAccountRulesModel
{
    /// <summary>Write timeout (AC-18).</summary>
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Timeout of the fetch.</summary>
    public static readonly TimeSpan FetchTimeout = ConnectionCoordinator.OperationTimeout;

    private readonly IParentHubGateway _gateway;
    private readonly TimeProvider _timeProvider;
    private readonly StateReplica<AccountRulesDto> _replica = new(snapshot => snapshot.Revision);
    private readonly Dictionary<Guid, PendingWrite> _pending = [];
    private readonly Lock _lock = new();
    private string? _selected;
    private AccountsLoadState _loadState = AccountsLoadState.NotAvailable;
    private long _generation;

    /// <summary>Creates the model and follows the gateway.</summary>
    public AccountRulesModel(IParentHubGateway gateway, TimeProvider timeProvider)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        gateway.Connected += () => Restart(SelectedAccountSid);
        gateway.Disconnected += OnDisconnected;
        gateway.AccountRulesChanged += snapshot => Apply(snapshot, Generation);
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public string? SelectedAccountSid
    {
        get
        {
            lock (_lock)
            {
                return _selected;
            }
        }
    }

    /// <inheritdoc />
    public AccountsLoadState LoadState
    {
        get
        {
            lock (_lock)
            {
                return _loadState;
            }
        }
    }

    /// <inheritdoc />
    public AccountRulesDto? Snapshot => _replica.Current;

    /// <summary>The last fetch started (awaited by tests).</summary>
    internal Task LastFetch { get; private set; } = Task.CompletedTask;

    private long Generation
    {
        get
        {
            lock (_lock)
            {
                return _generation;
            }
        }
    }

    /// <inheritdoc />
    public void SelectAccount(string? accountSid)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(SelectedAccountSid, accountSid))
        {
            Restart(accountSid);
        }
    }

    /// <inheritdoc />
    public Task<bool> AddEntryAsync() =>
        WriteAsync((client, id, sid, ct) => client.AddBreakTimeEntryAsync(id, sid, ct));

    /// <inheritdoc />
    public Task<bool> DeleteEntryAsync(long entryId) =>
        WriteAsync((client, id, sid, ct) => client.DeleteBreakTimeEntryAsync(id, sid, entryId, ct));

    /// <inheritdoc />
    public Task<bool> SetActiveAsync(long entryId, bool isActive) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryActiveAsync(id, sid, entryId, isActive, ct));

    /// <inheritdoc />
    public Task<bool> SetTimeAsync(long entryId, BreakTimeBoundary boundary, int minute) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryTimeAsync(id, sid, entryId, boundary, minute, ct));

    /// <inheritdoc />
    public Task<bool> SetDayAsync(long entryId, DayOfWeek day, bool isSelected) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryDayAsync(id, sid, entryId, day, isSelected, ct));

    /// <inheritdoc />
    public Task<bool> SetDisplayTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WriteAsync((client, id, sid, ct) => client.SetDisplayTextAsync(id, sid, text, ct));
    }

    private async Task<bool> WriteAsync(Func<IParentHubClient, Guid, string, CancellationToken, Task<StateWriteAckDto>> call)
    {
        var pending = new PendingWrite(Guid.NewGuid());
        string sid;
        lock (_lock)
        {
            if (_selected is null || _loadState != AccountsLoadState.Ready)
            {
                return false;
            }

            sid = _selected;
            _pending[pending.RequestId] = pending;
        }

        using var timeout = new CancellationTokenSource(WriteTimeout, _timeProvider);
        try
        {
            var ack = await _gateway.InvokeAsync((client, ct) => call(client, pending.RequestId, sid, ct), WriteTimeout).ConfigureAwait(false);
            Acknowledge(pending, ack.Revision);
            return await pending.Confirmation.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (HubException)
        {
            // Rejected by the service (e.g. "The entry no longer exists."): nothing was stored.
            return false;
        }
        catch (ParentHubNotConnectedException)
        {
            // Not sent; the fetch of the next connect shows the stored state.
            return false;
        }
        catch (Exception)
        {
            // Connection boundary (timeout, connection lost): the write may have been stored after all.
            RefetchIfConnected();
            return false;
        }
        finally
        {
            lock (_lock)
            {
                _pending.Remove(pending.RequestId);
            }
        }
    }

    private void Restart(string? accountSid)
    {
        long generation;
        bool fetch;
        lock (_lock)
        {
            generation = ++_generation;
            _selected = accountSid;
            _replica.Reset();
            fetch = accountSid is not null && _gateway.IsConnected;
            _loadState = fetch ? AccountsLoadState.Loading : AccountsLoadState.NotAvailable;
        }

        Changed?.Invoke();
        if (fetch)
        {
            LastFetch = FetchAsync(accountSid!, generation); // fetch implies a selection.
        }
    }

    private void OnDisconnected()
    {
        List<PendingWrite> failed;
        lock (_lock)
        {
            _generation++;
            _replica.Reset();
            _loadState = AccountsLoadState.NotAvailable;
            failed = [.. _pending.Values];
        }

        foreach (var write in failed)
        {
            write.Confirmation.TrySetResult(false);
        }

        Changed?.Invoke();
    }

    private void RefetchIfConnected()
    {
        if (_gateway.IsConnected && SelectedAccountSid is { } sid)
        {
            LastFetch = FetchAsync(sid, Generation);
        }
    }

    private async Task FetchAsync(string accountSid, long generation)
    {
        try
        {
            var snapshot = await _gateway.InvokeAsync((client, ct) => client.GetAccountRulesAsync(accountSid, ct), FetchTimeout)
                .ConfigureAwait(false);
            Apply(snapshot, generation);
        }
        catch (Exception)
        {
            // Connection boundary: no data until the next connect, account change or broadcast.
            lock (_lock)
            {
                if (generation != _generation || _loadState != AccountsLoadState.Loading)
                {
                    return;
                }

                _loadState = AccountsLoadState.NotAvailable;
            }

            Changed?.Invoke();
        }
    }

    private void Apply(AccountRulesDto snapshot, long generation)
    {
        List<PendingWrite> confirmed;
        lock (_lock)
        {
            if (generation != _generation
                || !StringComparer.OrdinalIgnoreCase.Equals(_selected, snapshot.AccountSid)
                || !_replica.TryApply(snapshot))
            {
                return;
            }

            _loadState = AccountsLoadState.Ready;
            confirmed = [.. _pending.Values.Where(w => w.RequestId == snapshot.LastChangeRequestId || w.AckRevision <= snapshot.Revision)];
        }

        foreach (var write in confirmed)
        {
            write.Confirmation.TrySetResult(true);
        }

        Changed?.Invoke();
    }

    private void Acknowledge(PendingWrite pending, long revision)
    {
        lock (_lock)
        {
            pending.AckRevision = revision;
            if (_replica.Revision < revision)
            {
                return;
            }
        }

        // The normal case: the broadcast arrived before the acknowledgement (ADR-010 §5).
        pending.Confirmation.TrySetResult(true);
    }

    private sealed class PendingWrite(Guid requestId)
    {
        public Guid RequestId { get; } = requestId;

        /// <summary>The acknowledged revision; <see cref="long.MaxValue"/> until the acknowledgement arrived.</summary>
        public long AckRevision { get; set; } = long.MaxValue;

        public TaskCompletionSource<bool> Confirmation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
