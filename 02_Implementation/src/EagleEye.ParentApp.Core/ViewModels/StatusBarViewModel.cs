using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Communication;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The status bar (US-002 AC-10): indicator green only when connected and confirmed as paired,
/// red otherwise, and a status text naming the host.
/// </summary>
public sealed class StatusBarViewModel : ObservableObject
{
    private bool _isConnected;
    private string _statusText = string.Empty;

    /// <summary>Creates the view model and follows the coordinator state.</summary>
    public StatusBarViewModel(IConnectionCoordinator coordinator, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Apply(coordinator.State);
        coordinator.StateChanged += state => dispatcher.Post(() => Apply(state));
    }

    /// <summary>Green indicator (connected) or red (anything else).</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    /// <summary>The localized status text.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Returns the status text for the state.</summary>
    internal static string Compose(ConnectionState state)
    {
        return state.Status switch
        {
            ConnectionStatus.PairedConnected => AppTexts.Format(AppTexts.StatusConnectedFormat, state.Host),
            ConnectionStatus.PairedDisconnected => AppTexts.Format(AppTexts.StatusNotConnectedFormat, state.Host),
            ConnectionStatus.NotPaired => AppTexts.StatusNotConnected,
            _ => AppTexts.Format(AppTexts.StatusConnectingFormat, state.Host),
        };
    }

    private void Apply(ConnectionState state)
    {
        IsConnected = state.Status == ConnectionStatus.PairedConnected;
        StatusText = Compose(state);
    }
}
