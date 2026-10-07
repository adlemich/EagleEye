using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// Thin wrapper over a SignalR <see cref="HubConnection"/> to <c>https://&lt;host&gt;:5443/hubs/parent</c>.
/// The trust policy is set on <b>both</b> the HTTP handler (negotiate, long polling) and the
/// WebSocket options; otherwise the WebSocket transport would fail or ignore the pin. The token is
/// sent as <c>Authorization: Bearer</c> (ADR-008 §5). Not unit-tested (real SignalR); verified manually.
/// </summary>
internal sealed class ParentHubClient : IParentHubClient
{
    private readonly HubConnection _connection;

    public ParentHubClient(HostAddress host, string? token, CertificateTrustPolicy trust)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(trust);

        var builder = new HubConnectionBuilder().WithUrl(host.ToParentHubUri(), options =>
        {
            if (token is not null)
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            }

            options.HttpMessageHandlerFactory = handler => ApplyTrust(handler, trust);
            options.WebSocketConfiguration = webSocket =>
                webSocket.RemoteCertificateValidationCallback = (_, certificate, _, _) => trust.Validate(certificate);
        });

        if (token is not null)
        {
            builder.WithAutomaticReconnect(new ParentReconnectPolicy());
        }

        _connection = builder.Build();
        _connection.Reconnecting += _ => Raise(Reconnecting);
        _connection.Reconnected += _ => Raise(Reconnected);
        _connection.Closed += _ => Raise(Closed);

        // Registered before StartAsync, so no broadcast is lost (coding guidelines §7.2).
        _connection.On<UserAccountListDto>(nameof(IParentClientCallback.OnUserAccountsChanged), snapshot => UserAccountsChanged?.Invoke(snapshot));
    }

    public event Action<UserAccountListDto>? UserAccountsChanged;

    public Task<UserAccountListDto> GetUserAccountsAsync(CancellationToken ct)
        => _connection.InvokeAsync<UserAccountListDto>(nameof(IParentHub.GetUserAccounts), ct);

    public Task<StateWriteAckDto> SetParentalControlAsync(Guid requestId, string accountSid, bool isUnderParentalControl, CancellationToken ct)
        => _connection.InvokeAsync<StateWriteAckDto>(nameof(IParentHub.SetParentalControl), requestId, accountSid, isUnderParentalControl, ct);

    public event Action? Reconnecting;

    public event Action? Reconnected;

    public event Action? Closed;

    public Task StartAsync(CancellationToken ct) => _connection.StartAsync(ct);

    public Task<PairingStatusDto> GetPairingStatusAsync(CancellationToken ct)
        => _connection.InvokeAsync<PairingStatusDto>(nameof(IParentHub.GetPairingStatus), ct);

    public Task StartPairingAsync(CancellationToken ct)
        => _connection.InvokeAsync(nameof(IParentHub.StartPairing), ct);

    public Task<PairingResultDto> SubmitPairingCodeAsync(string code, string deviceName, CancellationToken ct)
        => _connection.InvokeAsync<PairingResultDto>(nameof(IParentHub.SubmitPairingCode), code, deviceName, ct);

    public Task RemovePairedDeviceAsync(string deviceId, CancellationToken ct)
        => _connection.InvokeAsync(nameof(IParentHub.RemovePairedDevice), deviceId, ct);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private static HttpMessageHandler ApplyTrust(HttpMessageHandler handler, CertificateTrustPolicy trust)
    {
        switch (handler)
        {
            case HttpClientHandler clientHandler:
                clientHandler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) => trust.Validate(certificate);
                break;
            case SocketsHttpHandler socketsHandler:
                socketsHandler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) => trust.Validate(certificate);
                break;
        }

        return handler;
    }

    private static Task Raise(Action? handler)
    {
        handler?.Invoke();
        return Task.CompletedTask;
    }
}
