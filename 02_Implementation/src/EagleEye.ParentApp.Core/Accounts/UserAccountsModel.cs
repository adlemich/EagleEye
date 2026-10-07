using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.ParentApp.Core.Accounts;

/// <summary>
/// Client side of the state area "UserAccounts" (ADR-010, coding guidelines §7.5). On every confirmed
/// (re)connect it resets the replica, shows "Loading" and fetches; on disconnect it shows no data
/// (OQ-5) and fails pending writes; broadcasts are applied by the revision rule. A write is confirmed
/// by a snapshot with its request id, or with a revision at least the acknowledged one. Unknown
/// outcomes (timeout, connection loss) lead to a new fetch: at once if still connected, otherwise
/// by the fetch of the next connect.
/// </summary>
public sealed class UserAccountsModel : IUserAccountsModel
{
    /// <summary>Write timeout: the app reverts before 5 s (US-003 AC-16, plan D-5).</summary>
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Timeout of the fetch after a (re)connect.</summary>
    public static readonly TimeSpan FetchTimeout = ConnectionCoordinator.OperationTimeout;

    private readonly IParentHubGateway _gateway;
    private readonly TimeProvider _timeProvider;
    private readonly StateReplica<UserAccountListDto> _replica = new(snapshot => snapshot.Revision);
    private readonly Dictionary<Guid, PendingWrite> _pending = new();
    private readonly Lock _lock = new();
    private AccountsLoadState _loadState = AccountsLoadState.NotAvailable;
    private long _connection;

    /// <summary>Creates the model and follows the gateway.</summary>
    public UserAccountsModel(IParentHubGateway gateway, TimeProvider timeProvider)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        gateway.Connected += OnConnected;
        gateway.Disconnected += OnDisconnected;
        gateway.UserAccountsChanged += snapshot => Apply(snapshot, Connection);
        if (gateway.IsConnected)
        {
            OnConnected();
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

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
    public UserAccountListDto? Snapshot => _replica.Current;

    /// <summary>The last fetch started (awaited by tests).</summary>
    internal Task LastFetch { get; private set; } = Task.CompletedTask;

    private long Connection
    {
        get
        {
            lock (_lock)
            {
                return _connection;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetParentalControlAsync(string accountSid, bool isUnderParentalControl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        var pending = new PendingWrite(Guid.NewGuid());
        lock (_lock)
        {
            _pending[pending.RequestId] = pending;
        }

        using var timeout = new CancellationTokenSource(WriteTimeout, _timeProvider);
        try
        {
            var ack = await _gateway.InvokeAsync(
                (client, ct) => client.SetParentalControlAsync(pending.RequestId, accountSid, isUnderParentalControl, ct),
                WriteTimeout).ConfigureAwait(false);
            Acknowledge(pending, ack.Revision);
            return await pending.Confirmation.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (HubException)
        {
            // Rejected by the service: nothing was stored.
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

    private void OnConnected()
    {
        long connection;
        lock (_lock)
        {
            connection = ++_connection;
            _replica.Reset();
            _loadState = AccountsLoadState.Loading;
        }

        Changed?.Invoke();
        LastFetch = FetchAsync(connection);
    }

    private void OnDisconnected()
    {
        List<PendingWrite> failed;
        lock (_lock)
        {
            _connection++;
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
        if (_gateway.IsConnected)
        {
            LastFetch = FetchAsync(Connection);
        }
    }

    private async Task FetchAsync(long connection)
    {
        try
        {
            var snapshot = await _gateway.InvokeAsync((client, ct) => client.GetUserAccountsAsync(ct), FetchTimeout)
                .ConfigureAwait(false);
            Apply(snapshot, connection);
        }
        catch (Exception)
        {
            // Connection boundary: a failed fetch shows "No data" until a broadcast or the next connect.
            bool changed;
            lock (_lock)
            {
                changed = connection == _connection && _loadState == AccountsLoadState.Loading;
                if (changed)
                {
                    _loadState = AccountsLoadState.NotAvailable;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }
    }

    private void Apply(UserAccountListDto snapshot, long connection)
    {
        List<PendingWrite> confirmed;
        lock (_lock)
        {
            // A result of an earlier connection is not comparable with this connection's revisions.
            if (connection != _connection || !_replica.TryApply(snapshot))
            {
                return;
            }

            _loadState = AccountsLoadState.Ready;
            confirmed = [];
            foreach (var write in _pending.Values)
            {
                if (write.RequestId == snapshot.LastChangeRequestId || write.AckRevision <= snapshot.Revision)
                {
                    confirmed.Add(write);
                }
            }
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
