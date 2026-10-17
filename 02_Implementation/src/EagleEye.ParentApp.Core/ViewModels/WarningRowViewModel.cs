using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What a warning row needs from its section (<see cref="WarningSettingsViewModel"/>).</summary>
internal interface IWarningRowHost
{
    /// <summary>Queues an action on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Sends a write; shows the save-failed message on failure and clears the message on success. Never throws.</summary>
    Task<bool> SendAsync(Func<IWarningSettingsModel, Task<bool>> write);

    /// <summary>Shows the message of a refused level (AC-15).</summary>
    void ShowProblem(WarningEditProblem problem);
}

/// <summary>
/// One of the two fixed rows of the time-elapsed warnings (US-006 AC-13 to AC-16): the fixed label, the minutes before
/// the end (sent on <see cref="CommitMinutes"/>, checked against the confirmed other level) and the display text (sent on
/// <see cref="CommitText"/>; an unchanged default follows the parent app's language, ADR-016). Snapshots never overwrite
/// uncommitted typing or a pending write. UI thread only.
/// </summary>
public sealed class WarningRowViewModel : ObservableObject
{
    private readonly IWarningRowHost _host;
    private WarningSettingsDto _confirmed;
    private string _minutesText;
    private string _text;
    private bool _minutesDirty;
    private bool _textDirty;
    private int _pendingMinutes;

    internal WarningRowViewModel(WarningLevel level, WarningSettingsDto confirmed, IWarningRowHost host)
    {
        Level = level;
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _minutesText = AppBudgetText.Format(ConfirmedMinutes);
        _text = ConfirmedText;
    }

    /// <summary>The level of this row.</summary>
    public WarningLevel Level { get; }

    /// <summary>"Early warning" or "Final warning" (AC-13).</summary>
    public string Label => Level == WarningLevel.Early ? AppTexts.WarningsEarly : AppTexts.WarningsFinal;

    /// <summary>Maximum length of the text (AC-15).</summary>
    public int TextMaxLength => BreakTimeRules.MaxDisplayTextLength;

    /// <summary>The minutes before the end as typed or confirmed.</summary>
    public string MinutesText
    {
        get => _minutesText;
        set
        {
            if (SetProperty(ref _minutesText, value ?? string.Empty))
            {
                _minutesDirty = true;
            }
        }
    }

    /// <summary>The display text as typed or confirmed.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                _textDirty = BreakTimeRules.NormalizeLineBreaks(_text) != ConfirmedText;
            }
        }
    }

    /// <summary>The last write started by this row (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    private int ConfirmedMinutes => Level == WarningLevel.Early ? _confirmed.EarlyMinutes : _confirmed.FinalMinutes;

    private string? ConfirmedParentText => Level == WarningLevel.Early ? _confirmed.EarlyText : _confirmed.FinalText;

    private string ConfirmedText => KidTextEditing.ShownText(ConfirmedParentText, Kind, CultureInfo.CurrentUICulture);

    private DefaultTextKind Kind => Level == WarningLevel.Early ? DefaultTextKind.EarlyWarningText : DefaultTextKind.FinalWarningText;

    /// <summary>Commits changed minutes (leaving the field or Enter, AC-15).</summary>
    public void CommitMinutes()
    {
        if (!_minutesDirty)
        {
            return;
        }

        _minutesDirty = false;
        var problem = Check(out var minutes);
        if (problem != WarningEditProblem.None)
        {
            _host.ShowProblem(problem);
            _host.Post(Render);
            return;
        }

        SetProperty(ref _minutesText, AppBudgetText.Format(minutes), nameof(MinutesText));
        if (minutes != ConfirmedMinutes)
        {
            _pendingMinutes++;
            LastWrite = SendMinutesAsync(minutes);
        }
    }

    /// <summary>Commits the text (leaving the box, before the page changes; AC-15).</summary>
    public void CommitText()
    {
        if (!_textDirty)
        {
            return;
        }

        _textDirty = false;
        var confirmed = ConfirmedText;
        var send = KidTextEditing.Commit(_text, confirmed, ConfirmedParentText is null, Kind);
        if (send is null)
        {
            SetProperty(ref _text, confirmed, nameof(Text));
            return;
        }

        LastWrite = _host.SendAsync(model => model.SetTextAsync(Level, send));
    }

    /// <summary>Applies a snapshot to every field without typing or a pending write.</summary>
    internal void Update(WarningSettingsDto confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        _confirmed = confirmed;
        Render();
    }

    private WarningEditProblem Check(out int minutes)
    {
        if (!WarningEditRules.TryParseMinutes(_minutesText, out minutes))
        {
            return WarningEditProblem.InvalidMinutes;
        }

        var valid = Level == WarningLevel.Early
            ? WarningEditRules.IsValidPair(minutes, _confirmed.FinalMinutes)
            : WarningEditRules.IsValidPair(_confirmed.EarlyMinutes, minutes);
        return valid ? WarningEditProblem.None : WarningEditProblem.FinalNotLess;
    }

    private async Task SendMinutesAsync(int minutes)
    {
        await _host.SendAsync(model => model.SetMinutesAsync(Level, minutes));
        _host.Post(() =>
        {
            _pendingMinutes--;
            Render();
        });
    }

    private void Render()
    {
        if (!_minutesDirty && _pendingMinutes == 0)
        {
            SetProperty(ref _minutesText, AppBudgetText.Format(ConfirmedMinutes), nameof(MinutesText));
        }

        if (!_textDirty)
        {
            SetProperty(ref _text, ConfirmedText, nameof(Text));
        }
    }
}
