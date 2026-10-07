using EagleEye.ParentApp.Core.Data;
using EagleEye.Shared.Communication;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Communication;

/// <summary>
/// The parent app's connection and pairing state machine (US-002 plan, ADR-008). Pairing uses a
/// one-off trust-on-first-use connection; a paired connection pins the certificate, authenticates
/// with the token and reconnects automatically. The app reports <see cref="ConnectionStatus.PairedConnected"/>
/// only after the service confirmed the pairing for this connection (AC-20). Public operations are
/// serialized; connection events arrive on thread-pool threads.
/// </summary>
public sealed class ConnectionCoordinator(
    IParentHubClientFactory clientFactory,
    IPairingStore pairingStore,
    IPairedConnectionSink pairedConnection,
    TimeProvider timeProvider) : IConnectionCoordinator, IAsyncDisposable
{
    /// <summary>Upper bound for connecting and for each hub call.</summary>
    internal static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(15);

    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ConnectionState _state = ConnectionState.Initial;
    private PairingSession? _pairingSession;
    private PairedSession? _pairedSession;
    private CancellationTokenSource? _pairedLoopCts;
    private bool _disposed;

    /// <inheritdoc />
    public event Action<ConnectionState>? StateChanged;

    /// <inheritdoc />
    public ConnectionState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>The background connect loop of the paired connection (awaited by tests).</summary>
    internal Task PairedLoop { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public async Task<ConnectionState> InitializeAsync()
    {
        var pairing = await pairingStore.LoadAsync().ConfigureAwait(false);
        if (pairing is null)
        {
            SetState(ConnectionState.Initial);
        }
        else if (HostAddress.TryParse(pairing.Host, out var host))
        {
            StartPairedLoop(pairing, host);
        }
        else
        {
            await pairingStore.DeleteAsync().ConfigureAwait(false);
            SetState(ConnectionState.Initial);
        }

        return State;
    }

    /// <inheritdoc />
    public Task BeginPairingAsync(string? hostInput) => SerializedAsync(async () =>
    {
        if (State.Status != ConnectionStatus.NotPaired)
        {
            return;
        }

        if (!HostAddress.TryParse(hostInput, out var host))
        {
            SetState(ConnectionState.Initial with { Host = hostInput?.Trim(), LastMessage = ConnectionMessage.InvalidHost });
            return;
        }

        SetState(new ConnectionState(ConnectionStatus.PairingConnecting, host.Display, null, ConnectionMessage.None));
        var trust = CertificateTrustPolicy.TrustOnFirstUse();
        var client = clientFactory.Create(host, token: null, trust);
        try
        {
            using var timeout = CreateTimeout();
            await client.StartAsync(timeout.Token).ConfigureAwait(false);
            await client.StartPairingAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Connection boundary: any failure means the service is not reachable (AC-12).
            await client.DisposeAsync().ConfigureAwait(false);
            SetState(ConnectionState.Initial with { Host = host.Display, LastMessage = ConnectionMessage.Unreachable });
            return;
        }

        var session = new PairingSession(host, trust, client);
        client.Closed += () => OnPairingConnectionClosed(session);
        lock (_lock)
        {
            _pairingSession = session;
        }

        SetState(new ConnectionState(ConnectionStatus.AwaitingCode, host.Display, null, ConnectionMessage.None));
    });

    /// <inheritdoc />
    public Task RequestNewCodeAsync() => SerializedAsync(async () =>
    {
        if (CurrentPairingSession() is not { } session)
        {
            return;
        }

        try
        {
            using var timeout = CreateTimeout();
            await session.Client.StartPairingAsync(timeout.Token).ConfigureAwait(false);
            SetMessage(ConnectionMessage.None);
        }
        catch (Exception)
        {
            await AbortPairingAsync(session, ConnectionMessage.Unreachable).ConfigureAwait(false);
        }
    });

    /// <inheritdoc />
    public Task SubmitCodeAsync(string? code, string? deviceName) => SerializedAsync(async () =>
    {
        if (CurrentPairingSession() is not { } session)
        {
            return;
        }

        var name = PairingRules.NormalizeDeviceName(deviceName);
        if (!PairingRules.IsValidCodeFormat(code) || name is null)
        {
            SetMessage(name is null ? ConnectionMessage.DeviceNameRequired : ConnectionMessage.CodeFormat);
            return;
        }

        PairingResultDto result;
        try
        {
            using var timeout = CreateTimeout();
            result = await session.Client.SubmitPairingCodeAsync(code, name, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await AbortPairingAsync(session, ConnectionMessage.Unreachable).ConfigureAwait(false);
            return;
        }

        await CompletePairingAsync(session, result, name).ConfigureAwait(false);
    });

    /// <inheritdoc />
    public Task CancelPairingAsync() => SerializedAsync(async () =>
    {
        if (CurrentPairingSession() is { } session)
        {
            await AbortPairingAsync(session, ConnectionMessage.None).ConfigureAwait(false);
        }
    });

    /// <inheritdoc />
    public async Task<bool> RemovePairingAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            PairedSession? session;
            lock (_lock)
            {
                session = _pairedSession;
            }

            if (session is null || State.Status != ConnectionStatus.PairedConnected)
            {
                return false;
            }

            try
            {
                using var timeout = CreateTimeout();
                await session.Client.RemovePairedDeviceAsync(session.Pairing.DeviceId, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                SetMessage(ConnectionMessage.RemoveFailed);
                return false;
            }

            await StopPairedAsync().ConfigureAwait(false);
            await pairingStore.DeleteAsync().ConfigureAwait(false);
            SetState(ConnectionState.Initial);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (CurrentPairingSession() is { } pairing)
        {
            await pairing.Client.DisposeAsync().ConfigureAwait(false);
        }

        await StopPairedAsync().ConfigureAwait(false);
        _lifetime.Dispose();
        _gate.Dispose();
    }

    private async Task CompletePairingAsync(PairingSession session, PairingResultDto result, string deviceName)
    {
        if (result.Outcome != PairingOutcome.Success)
        {
            SetMessage(result.Outcome switch
            {
                PairingOutcome.WrongCode => ConnectionMessage.WrongCode,
                PairingOutcome.CodeExpired => ConnectionMessage.CodeExpired,
                PairingOutcome.InvalidDeviceName => ConnectionMessage.DeviceNameRequired,
                PairingOutcome.InvalidCodeFormat => ConnectionMessage.CodeFormat,
                _ => ConnectionMessage.NoPendingCode,
            });
            return;
        }

        var thumbprint = session.Trust.ObservedThumbprint;
        if (thumbprint is null || result.DeviceId is null || result.Token is null)
        {
            await AbortPairingAsync(session, ConnectionMessage.Unreachable).ConfigureAwait(false);
            return;
        }

        var pairing = new StoredPairing(
            session.Host.Display, result.DeviceId, deviceName, thumbprint, timeProvider.GetUtcNow(), result.Token);
        await pairingStore.SaveAsync(pairing).ConfigureAwait(false);
        await AbortPairingAsync(session, message: null).ConfigureAwait(false);
        StartPairedLoop(pairing, session.Host);
    }

    private async Task AbortPairingAsync(PairingSession session, ConnectionMessage? message)
    {
        lock (_lock)
        {
            _pairingSession = null;
        }

        await session.Client.DisposeAsync().ConfigureAwait(false);
        if (message is { } notPairedMessage)
        {
            SetState(ConnectionState.Initial with { Host = session.Host.Display, LastMessage = notPairedMessage });
        }
    }

    private void OnPairingConnectionClosed(PairingSession session)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_pairingSession, session))
            {
                return;
            }

            _pairingSession = null;
        }

        // Pairing connections do not reconnect: the pending code is bound to the lost connection.
        _ = session.Client.DisposeAsync().AsTask();
        SetState(ConnectionState.Initial with { Host = session.Host.Display, LastMessage = ConnectionMessage.Unreachable });
    }

    private void StartPairedLoop(StoredPairing pairing, HostAddress host)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        lock (_lock)
        {
            _pairedLoopCts = cts;
        }

        SetState(new ConnectionState(ConnectionStatus.PairedConnecting, pairing.Host, pairing.DeviceName, ConnectionMessage.None));
        PairedLoop = RunPairedLoopAsync(pairing, host, cts.Token);
    }

    private async Task RunPairedLoopAsync(StoredPairing pairing, HostAddress host, CancellationToken ct)
    {
        try
        {
            for (var attempt = 0; !await TryConnectPairedAsync(pairing, host, ct).ConfigureAwait(false); attempt++)
            {
                await Task.Delay(ConnectBackoff.GetDelay(attempt), timeProvider, ct).ConfigureAwait(false);
                SetStatus(ConnectionStatus.PairedConnecting);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Stopped (pairing removed or app closing).
        }
    }

    /// <summary>One connection attempt; returns <c>false</c> if it should be retried.</summary>
    private async Task<bool> TryConnectPairedAsync(StoredPairing pairing, HostAddress host, CancellationToken ct)
    {
        var trust = CertificateTrustPolicy.Pinned(pairing.CertificateThumbprint);
        var client = clientFactory.Create(host, pairing.Token, trust);
        PairingStatusDto status;
        try
        {
            using var timeout = CreateTimeout(ct);
            await client.StartAsync(timeout.Token).ConfigureAwait(false);
            status = await client.GetPairingStatusAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Retry boundary: unreachable, refused or pin mismatch. Cancellation ends the loop.
            await client.DisposeAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            SetState(State with
            {
                Status = ConnectionStatus.PairedDisconnected,
                LastMessage = trust.PinMismatch ? ConnectionMessage.CertificateChanged : ConnectionMessage.Unreachable,
            });
            return false;
        }

        if (!status.IsPaired)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            await ForgetLostPairingAsync(pairing).ConfigureAwait(false);
            return true;
        }

        AttachPairedClient(new PairedSession(pairing, host, client));
        return true;
    }

    private void AttachPairedClient(PairedSession session)
    {
        lock (_lock)
        {
            _pairedSession = session;
        }

        session.Client.Reconnecting += () => OnPairedReconnecting(session);
        session.Client.Reconnected += () => _ = OnPairedReconnectedAsync(session);
        session.Client.Closed += () => OnPairedClosed(session);
        SetState(State with { Status = ConnectionStatus.PairedConnected, LastMessage = ConnectionMessage.None });
        pairedConnection.SetConnected(session.Client);
    }

    private bool IsCurrent(PairedSession session)
    {
        lock (_lock)
        {
            return ReferenceEquals(_pairedSession, session);
        }
    }

    private void OnPairedReconnecting(PairedSession session)
    {
        if (IsCurrent(session))
        {
            pairedConnection.SetDisconnected();
            SetStatus(ConnectionStatus.PairedDisconnected);
        }
    }

    private async Task OnPairedReconnectedAsync(PairedSession session)
    {
        if (!IsCurrent(session))
        {
            return;
        }

        try
        {
            using var timeout = CreateTimeout();
            var status = await session.Client.GetPairingStatusAsync(timeout.Token).ConfigureAwait(false);
            if (status.IsPaired)
            {
                SetStatus(ConnectionStatus.PairedConnected);
                pairedConnection.SetConnected(session.Client);
                return;
            }
        }
        catch (Exception)
        {
            // The connection dropped again; Reconnecting or Closed follows.
            return;
        }

        await StopPairedAsync().ConfigureAwait(false);
        await ForgetLostPairingAsync(session.Pairing).ConfigureAwait(false);
    }

    private void OnPairedClosed(PairedSession session)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_pairedSession, session))
            {
                return;
            }

            _pairedSession = null;
        }

        pairedConnection.SetDisconnected();

        // The automatic reconnect never gives up, so this happens only when the server closed the
        // connection for good: start over with the connect loop.
        _ = session.Client.DisposeAsync().AsTask();
        StartPairedLoop(session.Pairing, session.Host);
    }

    private async Task ForgetLostPairingAsync(StoredPairing pairing)
    {
        pairedConnection.SetDisconnected();
        await pairingStore.DeleteAsync().ConfigureAwait(false);
        SetState(ConnectionState.Initial with { Host = pairing.Host, LastMessage = ConnectionMessage.PairingLost });
    }

    private async Task StopPairedAsync()
    {
        PairedSession? session;
        CancellationTokenSource? cts;
        lock (_lock)
        {
            session = _pairedSession;
            cts = _pairedLoopCts;
            _pairedSession = null;
            _pairedLoopCts = null;
        }

        pairedConnection.SetDisconnected();
        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await PairedLoop.ConfigureAwait(false);
            cts.Dispose();
        }

        if (session is not null)
        {
            await session.Client.DisposeAsync().ConfigureAwait(false);
        }
    }

    private PairingSession? CurrentPairingSession()
    {
        lock (_lock)
        {
            return _pairingSession;
        }
    }

    private async Task SerializedAsync(Func<Task> operation)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken ct = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, ct);
        cts.CancelAfter(OperationTimeout);
        return cts;
    }

    private void SetStatus(ConnectionStatus status) => SetState(State with { Status = status });

    private void SetMessage(ConnectionMessage message) => SetState(State with { LastMessage = message });

    private void SetState(ConnectionState state)
    {
        lock (_lock)
        {
            _state = state;
        }

        StateChanged?.Invoke(state);
    }

    private sealed record PairingSession(HostAddress Host, CertificateTrustPolicy Trust, IParentHubClient Client);

    private sealed record PairedSession(StoredPairing Pairing, HostAddress Host, IParentHubClient Client);
}
