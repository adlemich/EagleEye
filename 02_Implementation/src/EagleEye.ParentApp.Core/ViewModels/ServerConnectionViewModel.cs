using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Communication;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// "Server connection" section (US-002 AC-8, AC-11 to AC-30): host entry while not paired, code
/// and device-name entry while pairing, pairing details and "Remove pairing" when paired (enabled
/// only while connected, AC-30), and the last error message.
/// </summary>
public sealed class ServerConnectionViewModel : ObservableObject
{
    private readonly IConnectionCoordinator _coordinator;
    private readonly IDialogService _dialogs;
    private ConnectionState _state = ConnectionState.Initial;
    private string _hostInput = string.Empty;
    private string _codeInput = string.Empty;
    private string _deviceNameInput = Environment.MachineName;

    /// <summary>Creates the view model and follows the coordinator state.</summary>
    public ServerConnectionViewModel(IConnectionCoordinator coordinator, IDialogService dialogs, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        ConnectCommand = new AsyncRelayCommand(() => _coordinator.BeginPairingAsync(HostInput));
        PairCommand = new AsyncRelayCommand(SubmitCodeAsync);
        NewCodeCommand = new AsyncRelayCommand(_coordinator.RequestNewCodeAsync);
        CancelCommand = new AsyncRelayCommand(_coordinator.CancelPairingAsync);
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, () => CanRemove);

        Apply(coordinator.State);
        coordinator.StateChanged += state => dispatcher.Post(() => Apply(state));
    }

    /// <summary>Starts pairing with <see cref="HostInput"/>.</summary>
    public IAsyncRelayCommand ConnectCommand { get; }

    /// <summary>Submits <see cref="CodeInput"/> and <see cref="DeviceNameInput"/>.</summary>
    public IAsyncRelayCommand PairCommand { get; }

    /// <summary>Requests a new pairing code.</summary>
    public IAsyncRelayCommand NewCodeCommand { get; }

    /// <summary>Abandons the pairing in progress.</summary>
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>Asks for confirmation and removes the pairing (AC-26, AC-27).</summary>
    public IAsyncRelayCommand RemoveCommand { get; }

    /// <summary>The host entry (editable only when not paired, AC-25).</summary>
    public string HostInput
    {
        get => _hostInput;
        set => SetProperty(ref _hostInput, value);
    }

    /// <summary>The pairing code entry.</summary>
    public string CodeInput
    {
        get => _codeInput;
        set => SetProperty(ref _codeInput, value);
    }

    /// <summary>The device name entry, prefilled with the PC name.</summary>
    public string DeviceNameInput
    {
        get => _deviceNameInput;
        set => SetProperty(ref _deviceNameInput, value);
    }

    /// <summary>The localized pairing status.</summary>
    public string PairingStatusText => _state.PairingStatus switch
    {
        PairingStatus.NotPaired => AppTexts.PairingStatusNotPaired,
        PairingStatus.InProgress => AppTexts.PairingStatusInProgress,
        _ => AppTexts.PairingStatusPaired,
    };

    /// <summary>Host entry and "Connect" are shown (not paired).</summary>
    public bool IsNotPaired => _state.Status == ConnectionStatus.NotPaired;

    /// <summary>Connecting to start pairing; the section shows progress only.</summary>
    public bool IsPairingConnecting => _state.Status == ConnectionStatus.PairingConnecting;

    /// <summary>Code and device-name entry are shown.</summary>
    public bool IsAwaitingCode => _state.Status == ConnectionStatus.AwaitingCode;

    /// <summary>Pairing details (read-only host, device name) are shown.</summary>
    public bool IsPaired => _state.PairingStatus == PairingStatus.Paired;

    /// <summary>"Remove pairing" is enabled: paired and connected (AC-26, AC-30).</summary>
    public bool CanRemove => _state.Status == ConnectionStatus.PairedConnected;

    /// <summary>Paired but not connected: the AC-30 hint is shown.</summary>
    public bool ShowRemoveHint => IsPaired && !CanRemove;

    /// <summary>"Paired with: &lt;host&gt;" (read-only while paired, AC-25).</summary>
    public string PairedWithText => AppTexts.Format(AppTexts.PairedWithFormat, _state.Host);

    /// <summary>"This device: &lt;name&gt;".</summary>
    public string ThisDeviceText => AppTexts.Format(AppTexts.ThisDeviceFormat, _state.DeviceName);

    /// <summary>The localized last error or information, or <c>null</c>.</summary>
    public string? MessageText => ConnectionMessageText.Compose(_state);

    /// <summary>Whether <see cref="MessageText"/> is shown.</summary>
    public bool HasMessage => MessageText is not null;

    /// <summary>Asks for the host in a dialog and starts pairing if the parent confirms (AC-11).</summary>
    public async Task PromptForHostAsync()
    {
        var host = await _dialogs.PromptAsync(
            AppTexts.HostDialogTitle,
            AppTexts.HostLabel,
            AppTexts.ConnectButton,
            AppTexts.CancelButton,
            AppTexts.HostPlaceholder,
            _state.Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        HostInput = host.Trim();
        await _coordinator.BeginPairingAsync(HostInput);
    }

    private async Task SubmitCodeAsync()
    {
        await _coordinator.SubmitCodeAsync(CodeInput, DeviceNameInput);
        CodeInput = string.Empty;
    }

    private async Task RemoveAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            AppTexts.RemoveConfirmTitle,
            AppTexts.Format(AppTexts.RemoveConfirmMessageFormat, _state.Host),
            AppTexts.RemovePairingButton,
            AppTexts.CancelButton);
        if (confirmed)
        {
            await _coordinator.RemovePairingAsync();
        }
    }

    private void Apply(ConnectionState state)
    {
        _state = state;
        if (state.Status == ConnectionStatus.NotPaired)
        {
            HostInput = state.Host ?? string.Empty;
        }

        // All derived properties depend on the state.
        OnPropertyChanged(string.Empty);
        RemoveCommand.NotifyCanExecuteChanged();
    }
}
