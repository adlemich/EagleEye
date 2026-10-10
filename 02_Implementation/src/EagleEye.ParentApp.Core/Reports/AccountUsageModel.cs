using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Reports;

/// <summary>
/// Client side of the usage areas "UsageDay:{sid}:{day}" (ADR-010, ADR-012 §6) for the selected account. On every
/// confirmed (re)connect and on every account change it resets, shows "Loading" and fetches <c>GetAccountUsage</c>;
/// results of an earlier selection or connection are ignored (generation counter). Broadcasts of the selected
/// account are applied per day with the revision rule (<see cref="StateReplica{T}"/>); days older than 90 days
/// and past days without usage are dropped (AC-19). Disconnect → no data (US-003 OQ-5). Never polls.
/// </summary>
public sealed class AccountUsageModel : IAccountUsageModel
{
    /// <summary>Days kept: today and the 89 before (AC-19).</summary>
    public const int RetentionDays = 90;

    /// <summary>Timeout of the fetch.</summary>
    public static readonly TimeSpan FetchTimeout = ConnectionCoordinator.OperationTimeout;

    private readonly IParentHubGateway _gateway;
    private readonly Dictionary<DateOnly, StateReplica<DayUsageDto>> _days = [];
    private readonly Lock _lock = new();
    private string? _selected;
    private AccountsLoadState _loadState = AccountsLoadState.NotAvailable;
    private DateOnly? _serviceToday;
    private long _generation;

    /// <summary>Creates the model and follows the gateway.</summary>
    public AccountUsageModel(IParentHubGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        gateway.Connected += () => Restart(SelectedAccountSid);
        gateway.Disconnected += OnDisconnected;
        gateway.DayUsageChanged += OnDayUsageChanged;
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
    public DateOnly? ServiceToday
    {
        get
        {
            lock (_lock)
            {
                return _serviceToday;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DayUsageDto> Days
    {
        get
        {
            lock (_lock)
            {
                return [.. _days.Values.Select(d => d.Current!).OrderByDescending(d => d.Day)]; // Only applied replicas are kept.
            }
        }
    }

    /// <summary>The last fetch started (awaited by tests).</summary>
    internal Task LastFetch { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public void SelectAccount(string? accountSid)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(SelectedAccountSid, accountSid))
        {
            return;
        }

        Restart(accountSid);
    }

    private void Restart(string? accountSid)
    {
        long generation;
        bool fetch;
        lock (_lock)
        {
            generation = ++_generation;
            _selected = accountSid;
            _days.Clear();
            _serviceToday = null;
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
        lock (_lock)
        {
            _generation++;
            _days.Clear();
            _serviceToday = null;
            _loadState = AccountsLoadState.NotAvailable;
        }

        Changed?.Invoke();
    }

    private async Task FetchAsync(string accountSid, long generation)
    {
        AccountUsageDto usage;
        try
        {
            usage = await _gateway.InvokeAsync((client, ct) => client.GetAccountUsageAsync(accountSid, ct), FetchTimeout).ConfigureAwait(false);
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
            return;
        }

        lock (_lock)
        {
            if (generation != _generation)
            {
                return;
            }

            foreach (var day in usage.Days)
            {
                ApplyDay(day);
            }

            Advance(usage.ServiceToday);
            _loadState = AccountsLoadState.Ready;
        }

        Changed?.Invoke();
    }

    private void OnDayUsageChanged(DayUsageDto snapshot)
    {
        lock (_lock)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(_selected, snapshot.AccountSid))
            {
                return;
            }

            ApplyDay(snapshot);
            Advance(snapshot.ServiceToday);
            _loadState = AccountsLoadState.Ready;
        }

        Changed?.Invoke();
    }

    private void ApplyDay(DayUsageDto day)
    {
        var replica = _days.GetValueOrDefault(day.Day) ?? new StateReplica<DayUsageDto>(d => d.Revision);
        if (replica.TryApply(day))
        {
            _days[day.Day] = replica;
        }
    }

    /// <summary>Moves "today" forward (midnight) and drops days that must not be shown (AC-19).</summary>
    private void Advance(DateOnly serviceToday)
    {
        if (_serviceToday is not { } current || serviceToday > current)
        {
            _serviceToday = serviceToday;
        }

        var today = _serviceToday!.Value; // Set just above.
        var oldest = today.AddDays(1 - RetentionDays);
        foreach (var (day, replica) in _days.ToList())
        {
            if (day < oldest || (day != today && replica.Current!.Apps.Count == 0))
            {
                _days.Remove(day);
            }
        }
    }
}
