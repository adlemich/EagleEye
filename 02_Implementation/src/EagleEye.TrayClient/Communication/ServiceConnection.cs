using EagleEye.Shared.Constants;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace EagleEye.TrayClient.Communication;

/// <summary>
/// SignalR connection to the service's tray hub on localhost. Keeps reconnecting for as long
/// as the tray client runs: initial connect via <see cref="ConnectBackoff"/>, a lost connection
/// via <see cref="ServiceReconnectPolicy"/>, and a fully closed connection by restarting the
/// connect loop.
/// </summary>
public sealed class ServiceConnection : IServiceConnection
{
    private readonly HubConnection _hubConnection;
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Creates the connection. Call <see cref="ConnectAsync"/> to start it.</summary>
    /// <param name="serviceBaseUrl">Base URL of the service, e.g. <see cref="ServiceDefaults.LocalBaseUrl"/>.</param>
    public ServiceConnection(string serviceBaseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceBaseUrl);

        ServerAddress = new Uri(serviceBaseUrl).Authority;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(serviceBaseUrl + HubRoutes.Tray)
            .WithAutomaticReconnect(new ServiceReconnectPolicy())
            .Build();

        _hubConnection.Reconnecting += _ => RaiseConnectionChanged(false);
        _hubConnection.Reconnected += _ => RaiseConnectionChanged(true);
        _hubConnection.Closed += OnClosed;
    }

    /// <inheritdoc />
    public event Action<bool>? ConnectionChanged;

    /// <inheritdoc />
    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public string ServerAddress { get; }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        var token = linked.Token;

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _hubConnection.StartAsync(token).ConfigureAwait(false);
                    await RaiseConnectionChanged(true).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Retry-loop boundary: the service may be stopped, not yet started or
                    // restarting. Any failure to connect is retried after a back-off delay.
                    await Task.Delay(ConnectBackoff.GetDelay(attempt), token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Shutting down: stop trying.
        }
    }

    /// <inheritdoc />
    public Task<ServiceVersionDto> GetServiceVersionAsync(CancellationToken ct)
    {
        return _hubConnection.InvokeAsync<ServiceVersionDto>(nameof(ITrayHub.GetServiceVersion), ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _hubConnection.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private Task OnClosed(Exception? exception)
    {
        RaiseConnectionChanged(false);

        // The automatic reconnect gave up or the server closed the connection: start over,
        // unless the tray client itself is shutting down.
        if (!_lifetime.IsCancellationRequested)
        {
            _ = Task.Run(() => ConnectAsync(CancellationToken.None));
        }

        return Task.CompletedTask;
    }

    private Task RaiseConnectionChanged(bool connected)
    {
        ConnectionChanged?.Invoke(connected);
        return Task.CompletedTask;
    }
}
