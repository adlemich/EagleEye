using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// Holds the current confirmed paired connection (set by <see cref="ConnectionCoordinator"/> through
/// <see cref="IPairedConnectionSink"/>) and offers it to feature models (<see cref="IParentHubGateway"/>).
/// It subscribes to a client's broadcasts once and unsubscribes from the previous client when the
/// client changes; broadcasts are forwarded only from the current client while connected. Thread-safe.
/// </summary>
public sealed class ParentHubGateway(TimeProvider timeProvider) : IParentHubGateway, IPairedConnectionSink
{
    private readonly Lock _lock = new();
    private IParentHubClient? _subscribed;
    private Action<UserAccountListDto>? _subscription;
    private IParentHubClient? _connected;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public event Action<UserAccountListDto>? UserAccountsChanged;

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _connected is not null;
            }
        }
    }

    /// <inheritdoc />
    public void SetConnected(IParentHubClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_lock)
        {
            if (!ReferenceEquals(_subscribed, client))
            {
                if (_subscribed is not null)
                {
                    _subscribed.UserAccountsChanged -= _subscription;
                }

                _subscription = snapshot => Forward(client, snapshot);
                client.UserAccountsChanged += _subscription;
                _subscribed = client;
            }

            _connected = client;
        }

        Connected?.Invoke();
    }

    /// <inheritdoc />
    public void SetDisconnected()
    {
        bool wasConnected;
        lock (_lock)
        {
            wasConnected = _connected is not null;
            _connected = null;
        }

        if (wasConnected)
        {
            Disconnected?.Invoke();
        }
    }

    /// <inheritdoc />
    public async Task<T> InvokeAsync<T>(Func<IParentHubClient, CancellationToken, Task<T>> call, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(call);
        IParentHubClient client;
        lock (_lock)
        {
            client = _connected ?? throw new ParentHubNotConnectedException();
        }

        using var cts = new CancellationTokenSource(timeout, timeProvider);
        return await call(client, cts.Token).ConfigureAwait(false);
    }

    private void Forward(IParentHubClient sender, UserAccountListDto snapshot)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _connected))
            {
                return;
            }
        }

        UserAccountsChanged?.Invoke(snapshot);
    }
}
