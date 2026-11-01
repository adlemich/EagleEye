using System.Globalization;
using EagleEye.Shared.Models;
using EagleEye.TrayClient.Communication;
using EagleEye.TrayClient.Overlay;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Owns the tray icon: shows the connection state (green/red, AC-6 to AC-9) and offers the
/// "About" entry that queries the server version live (AC-11, AC-12). Shows pairing codes
/// sent by the service in a topmost window (US-002 AC-14), and the kid messages of the service (US-005 AC-30, US-006
/// AC-34: one dialog at a time, a newer message replaces the text). Since US-010 it runs the remaining-time overlay and offers
/// the overlay settings in the tray menu, which opens with the right and the left mouse button (AC-15).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan VersionQueryTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceConnection _connection;
    private readonly SynchronizationContext _uiContext;
    private readonly ContextMenuStrip _menu;
    private readonly OverlaySettingsModel _overlaySettings;
    private readonly OverlayController _overlay;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _connectedIcon;
    private readonly Icon _disconnectedIcon;
    private bool _aboutOpen;
    private PairingCodeDialog? _pairingDialog;
    private KidMessageDialog? _kidDialog;
    private KidMessagePresenter? _presenter;

    /// <summary>Creates the tray icon and starts following the connection state.</summary>
    public TrayApplicationContext(IServiceConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;

        // Creating the first control installs the WinForms synchronization context.
        _overlaySettings = new OverlaySettingsModel(new RegistryOverlaySettingsStore());
        _menu = TrayMenu.Build(_overlaySettings, OnAboutClicked, CultureInfo.CurrentUICulture);
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _overlay = new OverlayController(_overlaySettings, TimeProvider.System);

        _connectedIcon = TrayIcons.Load(TrayIcons.Connected, SystemInformation.SmallIconSize);
        _disconnectedIcon = TrayIcons.Load(TrayIcons.Disconnected, SystemInformation.SmallIconSize);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        ApplyConnectionState(_connection.IsConnected);

        _connection.ConnectionChanged += OnConnectionChanged;
        _connection.PairingCodeReceived += OnPairingCodeReceived;
        _connection.RemainingTimeReceived += OnRemainingTimeReceived;

        // US-005/US-006 (ADR-014 §2): the decision is synchronous, the dialog is created or updated on the UI thread.
        _presenter = new KidMessagePresenter(
            content => _uiContext.Post(_ => ShowKidMessage(content), null),
            content => _uiContext.Post(_ => ReplaceKidMessage(content), null));
        _connection.KidMessageHandler = _presenter.Present;
        Application.ApplicationExit += OnApplicationExit;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.ConnectionChanged -= OnConnectionChanged;
            _connection.PairingCodeReceived -= OnPairingCodeReceived;
            _connection.RemainingTimeReceived -= OnRemainingTimeReceived;
            _connection.KidMessageHandler = null;
            _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
            _overlay.Dispose();
            _kidDialog?.Dispose();
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

    private void OnRemainingTimeReceived(RemainingTimeDto model)
    {
        // Raised on a thread-pool thread by SignalR: marshal to the UI thread.
        _uiContext.Post(_ => _overlay.OnRemainingTime(model), null);
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        // AC-15 (TI-10): the left button opens the same menu as the right button.
        if (e.Button == MouseButtons.Left)
        {
            TrayMenu.ShowFromLeftClick(_notifyIcon, _menu);
        }
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

    private void ShowKidMessage(KidMessageContent content)
    {
        var dialog = new KidMessageDialog(content);
        dialog.FormClosed += (_, _) =>
        {
            _kidDialog = null;
            dialog.Dispose();
            _presenter?.DialogClosed();
        };
        _kidDialog = dialog;
        dialog.Show();
        ForegroundHelper.BringToFront(dialog);
    }

    private void ReplaceKidMessage(KidMessageContent content)
    {
        if (_kidDialog is { } dialog)
        {
            dialog.UpdateText(content);
            return;
        }

        // The kid closed the dialog at the same moment: show the newer message in a new dialog.
        _presenter?.DialogReopened();
        ShowKidMessage(content);
    }

    private void ApplyConnectionState(bool connected)
    {
        _notifyIcon.Icon = connected ? _connectedIcon : _disconnectedIcon;
        _notifyIcon.Text = connected ? TrayTexts.TooltipConnected : TrayTexts.TooltipDisconnected;
        if (!connected)
        {
            _overlay.OnDisconnected();
        }
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
