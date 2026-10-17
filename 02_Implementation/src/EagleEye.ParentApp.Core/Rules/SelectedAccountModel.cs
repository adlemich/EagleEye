using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of a per-account state area (ADR-010, coding guidelines §7.5) for the account selected on the Rules page,
/// shared by "AccountRules:{sid}" (US-005) and "AppRules:{sid}" (US-006). On every confirmed (re)connect and every account
/// change it resets, shows "Loading" and fetches the snapshot; results of an earlier selection or connection are ignored
/// (generation counter). Broadcasts of the selected account are applied with the revision rule. A write is confirmed by
/// a snapshot with its request id or with a revision at least the acknowledged one; timeout <see cref="WriteTimeout"/>
/// (the app reverts before 5 s); unknown outcomes lead to a new fetch. Disconnect → no data. Never polls.
/// </summary>
/// <typeparam name="TSnapshot">The snapshot DTO of the area.</typeparam>
public abstract class SelectedAccountModel<TSnapshot>
    where TSnapshot : class
{
    /// <summary>Write timeout (US-005 AC-18, US-006 AC-12).</summary>
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Timeout of the fetch.</summary>
    public static readonly TimeSpan FetchTimeout = ConnectionCoordinator.OperationTimeout;

    private readonly IParentHubGateway _gateway;
    private readonly TimeProvider _timeProvider;
    private readonly StateReplica<TSnapshot> _replica;
    private readonly Dictionary<Guid, PendingWrite> _pending = [];
    private readonly Lock _lock = new();
    private string? _selected;
    private AccountsLoadState _loadState = AccountsLoadState.NotAvailable;
    private long _generation;

    /// <summary>Creates the model and follows the gateway's connection.</summary>
    protected SelectedAccountModel(IParentHubGateway gateway, TimeProvider timeProvider, Func<TSnapshot, long> revisionOf)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _replica = new StateReplica<TSnapshot>(revisionOf);
        gateway.Connected += () => Restart(SelectedAccountSid);
        gateway.Disconnected += OnDisconnected;
    }

    /// <summary>Raised (on any thread) when the selection, the load state or the snapshot changed.</summary>
    public event Action? Changed;

    /// <summary>The selected account's SID, or null.</summary>
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

    /// <summary>Whether the snapshot of the selected account is available.</summary>
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

    /// <summary>The last applied snapshot of the selected account, or null.</summary>
    public TSnapshot? Snapshot => _replica.Current;

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

    /// <summary>Selects an account (or none) and fetches its snapshot while connected.</summary>
    public void SelectAccount(string? accountSid)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(SelectedAccountSid, accountSid))
        {
            Restart(accountSid);
        }
    }

    /// <summary>Fetches the snapshot of the account.</summary>
    protected abstract Task<TSnapshot> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct);

    /// <summary>The account of a snapshot.</summary>
    protected abstract string AccountSidOf(TSnapshot snapshot);

    /// <summary>The request id of the write that produced a snapshot.</summary>
    protected abstract Guid? LastRequestOf(TSnapshot snapshot);

    /// <summary>A broadcast of the area: applied if it belongs to the selected account and is newer.</summary>
    protected void OnBroadcast(TSnapshot snapshot) => Apply(snapshot, Generation);

    /// <summary>Sends a write for the selected account; <c>true</c> when a snapshot confirming it was applied.</summary>
    protected async Task<bool> WriteAsync(Func<IParentHubClient, Guid, string, CancellationToken, Task<StateWriteAckDto>> call)
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
            var snapshot = await _gateway.InvokeAsync((client, ct) => FetchAsync(client, accountSid, ct), FetchTimeout).ConfigureAwait(false);
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

    private void Apply(TSnapshot snapshot, long generation)
    {
        List<PendingWrite> confirmed;
        lock (_lock)
        {
            if (generation != _generation
                || !StringComparer.OrdinalIgnoreCase.Equals(_selected, AccountSidOf(snapshot))
                || !_replica.TryApply(snapshot))
            {
                return;
            }

            _loadState = AccountsLoadState.Ready;
            var revision = _replica.Revision;
            var requestId = LastRequestOf(snapshot);
            confirmed = [.. _pending.Values.Where(w => w.RequestId == requestId || w.AckRevision <= revision)];
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
