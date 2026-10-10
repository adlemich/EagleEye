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
    private Action<UserAccountListDto>? _accountsSubscription;
    private Action<DayUsageDto>? _usageSubscription;
    private Action<AccountRulesDto>? _rulesSubscription;
    private IParentHubClient? _connected;

    /// <inheritdoc />
    public event Action? Connected;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public event Action<UserAccountListDto>? UserAccountsChanged;

    /// <inheritdoc />
    public event Action<DayUsageDto>? DayUsageChanged;

    /// <inheritdoc />
    public event Action<AccountRulesDto>? AccountRulesChanged;

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
                    _subscribed.UserAccountsChanged -= _accountsSubscription;
                    _subscribed.DayUsageChanged -= _usageSubscription;
                    _subscribed.AccountRulesChanged -= _rulesSubscription;
                }

                _accountsSubscription = snapshot => Forward(client, () => UserAccountsChanged?.Invoke(snapshot));
                _usageSubscription = snapshot => Forward(client, () => DayUsageChanged?.Invoke(snapshot));
                client.UserAccountsChanged += _accountsSubscription;
                client.DayUsageChanged += _usageSubscription;
                _rulesSubscription = snapshot => Forward(client, () => AccountRulesChanged?.Invoke(snapshot));
                client.AccountRulesChanged += _rulesSubscription;
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

    private void Forward(IParentHubClient sender, Action raise)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _connected))
            {
                return;
            }
        }

        raise();
    }
}
