using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The section "Time-elapsed warnings" of the Settings page (US-006 AC-13 to AC-17): two fixed rows (early, final), the
/// page states of US-005 AC-6 and one message line for refused edits and failed writes. Edits are saved when the parent
/// leaves a field, and before the page changes (<see cref="FlushPendingEdits"/>). Follows the model on the UI thread.
/// </summary>
public sealed class WarningSettingsViewModel : ObservableObject, IWarningRowHost
{
    private readonly IWarningSettingsModel _model;
    private readonly IUiDispatcher _dispatcher;
    private AccountsLoadState _state = AccountsLoadState.NotAvailable;
    private WarningRowViewModel? _early;
    private WarningRowViewModel? _final;
    private string _errorText = string.Empty;

    /// <summary>Creates the section and follows the model.</summary>
    public WarningSettingsViewModel(IWarningSettingsModel model, IUiDispatcher dispatcher)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Apply();
        model.Changed += () => dispatcher.Post(Apply);
    }

    /// <summary>The early warning row, or null before the first snapshot.</summary>
    public WarningRowViewModel? Early
    {
        get => _early;
        private set => SetProperty(ref _early, value);
    }

    /// <summary>The final warning row, or null before the first snapshot.</summary>
    public WarningRowViewModel? Final
    {
        get => _final;
        private set => SetProperty(ref _final, value);
    }

    /// <summary>Whether the settings are available.</summary>
    public AccountsLoadState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>The table is shown.</summary>
    public bool IsReady => State == AccountsLoadState.Ready;

    /// <summary>"Loading …" or "No data available", empty when ready.</summary>
    public string StatusText => State switch
    {
        AccountsLoadState.Loading => AppTexts.AccountsLoading,
        AccountsLoadState.NotAvailable => AppTexts.AccountsNoData,
        _ => string.Empty,
    };

    /// <summary>The status text is shown.</summary>
    public bool HasStatus => !IsReady;

    /// <summary>The message of the last refused edit or failed write; cleared by the next successful change.</summary>
    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Whether a message is shown.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>Commits typed minutes and texts; called before the page changes.</summary>
    public void FlushPendingEdits()
    {
        foreach (var row in new[] { Early, Final })
        {
            row?.CommitMinutes();
            row?.CommitText();
        }
    }

    /// <inheritdoc />
    void IWarningRowHost.Post(Action action) => _dispatcher.Post(action);

    /// <inheritdoc />
    async Task<bool> IWarningRowHost.SendAsync(Func<IWarningSettingsModel, Task<bool>> write)
    {
        var ok = await write(_model);
        _dispatcher.Post(() =>
        {
            ErrorText = ok ? string.Empty : AppTexts.AccountSaveFailed;
            Apply();
        });
        return ok;
    }

    /// <inheritdoc />
    void IWarningRowHost.ShowProblem(WarningEditProblem problem) =>
        ErrorText = problem == WarningEditProblem.InvalidMinutes ? AppTexts.WarningsInvalidMinutes : AppTexts.WarningsFinalNotLess;

    private void Apply()
    {
        State = _model.LoadState;
        if (!IsReady)
        {
            return;
        }

        var snapshot = _model.Snapshot!; // Ready always has a snapshot.
        if (Early is null)
        {
            Early = new WarningRowViewModel(WarningLevel.Early, snapshot, this);
            Final = new WarningRowViewModel(WarningLevel.Final, snapshot, this);
            return;
        }

        Early.Update(snapshot);
        Final!.Update(snapshot); // Both rows are created together.
    }
}
