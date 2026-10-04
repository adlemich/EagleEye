using EagleEye.TrayClient.Communication;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Owns the tray icon: shows the connection state (green/red, AC-6 to AC-9) and offers the
/// "About" entry that queries the server version live (AC-11, AC-12). Shows pairing codes
/// sent by the service in a topmost window (US-002 AC-14).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan VersionQueryTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceConnection _connection;
    private readonly SynchronizationContext _uiContext;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _connectedIcon;
    private readonly Icon _disconnectedIcon;
    private bool _aboutOpen;
    private PairingCodeDialog? _pairingDialog;

    /// <summary>Creates the tray icon and starts following the connection state.</summary>
    public TrayApplicationContext(IServiceConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;

        // Creating the first control installs the WinForms synchronization context.
        _menu = new ContextMenuStrip();
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _menu.Items.Add(TrayTexts.AboutMenuItem, null, OnAboutClicked);

        _connectedIcon = TrayIcons.Load(TrayIcons.Connected, SystemInformation.SmallIconSize);
        _disconnectedIcon = TrayIcons.Load(TrayIcons.Disconnected, SystemInformation.SmallIconSize);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
        };
        ApplyConnectionState(_connection.IsConnected);

        _connection.ConnectionChanged += OnConnectionChanged;
        _connection.PairingCodeReceived += OnPairingCodeReceived;
        Application.ApplicationExit += OnApplicationExit;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.ConnectionChanged -= OnConnectionChanged;
            _connection.PairingCodeReceived -= OnPairingCodeReceived;
            _pairingDialog?.Dispose();
            Application.ApplicationExit -= OnApplicationExit;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _connectedIcon.Dispose();
            _disconnectedIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnConnectionChanged(bool connected)
    {
        // Raised on a thread-pool thread by SignalR: marshal to the UI thread.
        _uiContext.Post(_ => ApplyConnectionState(connected), null);
    }

    private void OnPairingCodeReceived(string code)
    {
        // Raised on a thread-pool thread by SignalR: marshal to the UI thread.
        _uiContext.Post(_ => ShowPairingCode(code), null);
    }

    private void ShowPairingCode(string code)
    {
        // A new code replaces an open window (the previous code is no longer valid).
        // Closing a modeless form also disposes it.
        _pairingDialog?.Close();
        var dialog = new PairingCodeDialog(code);
        dialog.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_pairingDialog, dialog))
            {
                _pairingDialog = null;
            }
        };
        _pairingDialog = dialog;
        dialog.Show();
        dialog.Activate();
    }

    private void ApplyConnectionState(bool connected)
    {
        _notifyIcon.Icon = connected ? _connectedIcon : _disconnectedIcon;
        _notifyIcon.Text = connected ? TrayTexts.TooltipConnected : TrayTexts.TooltipDisconnected;
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        // async void is required for WinForms event handlers; failures are handled inside
        // TryGetServiceVersionAsync, so nothing escapes to the message loop.
        if (_aboutOpen)
        {
            return;
        }

        _aboutOpen = true;
        try
        {
            var version = await TryGetServiceVersionAsync();
            using var dialog = new AboutDialog(AboutText.Compose(version, _connection.ServerAddress));
            dialog.ShowDialog();
        }
        finally
        {
            _aboutOpen = false;
        }
    }

    private async Task<string?> TryGetServiceVersionAsync()
    {
        if (!_connection.IsConnected)
        {
            return null;
        }

        using var timeout = new CancellationTokenSource(VersionQueryTimeout);
        try
        {
            var dto = await _connection.GetServiceVersionAsync(timeout.Token);
            return dto.Version;
        }
        catch (Exception ex) when (ex is HubException or InvalidOperationException or OperationCanceledException or IOException)
        {
            // Connection dropped or the query timed out: the dialog shows the connection error.
            return null;
        }
    }

    private void OnApplicationExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
    }
}
