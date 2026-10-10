using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What a break-time row needs from its page (<see cref="RulesViewModel"/>).</summary>
internal interface IBreakTimeRowHost
{
    /// <summary>Queues an action on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Sends a write; shows the AC-18 message on failure and clears the message on success. Never throws.</summary>
    Task<bool> SendAsync(Func<IAccountRulesModel, Task<bool>> write);

    /// <summary>Shows the message of a refused edit (AC-9 to AC-11).</summary>
    void ShowProblem(EditProblem problem);
}

/// <summary>
/// One row of the break-time table (US-005 AC-3, AC-8 to AC-14), edited in place. Checkboxes are sent at the click and
/// show the requested value while the write is pending, the confirmed one afterwards (ADR-010 §5). Time fields are
/// sent on <see cref="CommitStart"/>/<see cref="CommitEnd"/> (leaving the field, Enter); an invalid time returns to the
/// confirmed value with a message. Snapshots (<see cref="Update"/>) never overwrite a field with uncommitted typing or
/// a pending write (AC-19). Rows are merged by <see cref="EntryId"/>, never replaced. UI thread only.
/// </summary>
public sealed class BreakTimeRowViewModel : ObservableObject
{
    private readonly IBreakTimeRowHost _host;
    private readonly Dictionary<string, int> _pending = [];
    private BreakTimeEntryDto _confirmed;
    private bool _isActive;
    private BreakTimeDays _days;
    private string _startText;
    private string _endText;
    private bool _startDirty;
    private bool _endDirty;

    internal BreakTimeRowViewModel(BreakTimeEntryDto confirmed, IBreakTimeRowHost host)
    {
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _isActive = confirmed.IsActive;
        _days = confirmed.Days;
        _startText = TimeOfDayText.Format(confirmed.StartMinute);
        _endText = TimeOfDayText.Format(confirmed.EndMinute);
        DeleteCommand = new RelayCommand(() => LastWrite = _host.SendAsync(model => model.DeleteEntryAsync(EntryId)));
    }

    /// <summary>The entry's id.</summary>
    public long EntryId => _confirmed.EntryId;

    /// <summary>The trash can (AC-13): deletes at once, without confirmation.</summary>
    public IRelayCommand DeleteCommand { get; }

    /// <summary>The switch "On/Off" (AC-14).</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                Send(nameof(IsActive), model => model.SetActiveAsync(EntryId, value));
            }
        }
    }

    /// <summary>The start time as typed or confirmed (HH:MM).</summary>
    public string StartText
    {
        get => _startText;
        set
        {
            if (SetProperty(ref _startText, value ?? string.Empty))
            {
                _startDirty = true;
            }
        }
    }

    /// <summary>The end time as typed or confirmed (HH:MM).</summary>
    public string EndText
    {
        get => _endText;
        set
        {
            if (SetProperty(ref _endText, value ?? string.Empty))
            {
                _endDirty = true;
            }
        }
    }

#pragma warning disable CS1591 // One checkbox per weekday (AC-3), Monday first.
    public bool Monday { get => Has(DayOfWeek.Monday); set => SetDay(DayOfWeek.Monday, value); }

    public bool Tuesday { get => Has(DayOfWeek.Tuesday); set => SetDay(DayOfWeek.Tuesday, value); }

    public bool Wednesday { get => Has(DayOfWeek.Wednesday); set => SetDay(DayOfWeek.Wednesday, value); }

    public bool Thursday { get => Has(DayOfWeek.Thursday); set => SetDay(DayOfWeek.Thursday, value); }

    public bool Friday { get => Has(DayOfWeek.Friday); set => SetDay(DayOfWeek.Friday, value); }

    public bool Saturday { get => Has(DayOfWeek.Saturday); set => SetDay(DayOfWeek.Saturday, value); }

    public bool Sunday { get => Has(DayOfWeek.Sunday); set => SetDay(DayOfWeek.Sunday, value); }
#pragma warning restore CS1591

    /// <summary>Whether a time field has typing that was not committed yet.</summary>
    public bool HasUncommittedTime => _startDirty || _endDirty;

    /// <summary>The last write started by this row (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    /// <summary>Commits a changed start time (leaving the field or Enter, AC-8).</summary>
    public void CommitStart() => CommitTime(BreakTimeBoundary.Start);

    /// <summary>Commits a changed end time (leaving the field or Enter, AC-8).</summary>
    public void CommitEnd() => CommitTime(BreakTimeBoundary.End);

    /// <summary>Applies the confirmed entry of a snapshot to every field without typing or a pending write.</summary>
    internal void Update(BreakTimeEntryDto confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        _confirmed = confirmed;
        Render();
    }

    private void Render()
    {
        if (!_pending.ContainsKey(nameof(IsActive)) && _isActive != _confirmed.IsActive)
        {
            _isActive = _confirmed.IsActive;
            OnPropertyChanged(nameof(IsActive));
        }

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var flag = BreakTimeRules.ToDays(day);
            if (!_pending.ContainsKey(day.ToString()) && (_days & flag) != (_confirmed.Days & flag))
            {
                _days = (_days & ~flag) | (_confirmed.Days & flag);
                OnPropertyChanged(day.ToString());
            }
        }

        if (!_startDirty && !_pending.ContainsKey(nameof(StartText)))
        {
            SetProperty(ref _startText, TimeOfDayText.Format(_confirmed.StartMinute), nameof(StartText));
        }

        if (!_endDirty && !_pending.ContainsKey(nameof(EndText)))
        {
            SetProperty(ref _endText, TimeOfDayText.Format(_confirmed.EndMinute), nameof(EndText));
        }
    }

    private bool Has(DayOfWeek day) => (_days & BreakTimeRules.ToDays(day)) != BreakTimeDays.None;

    private void SetDay(DayOfWeek day, bool isSelected)
    {
        if (Has(day) == isSelected)
        {
            return;
        }

        if (BreakTimeEditRules.CheckDay(_days, day, isSelected) is var problem and not EditProblem.None)
        {
            // The checkbox stays ticked (AC-11); re-read after the binding has finished writing.
            _host.ShowProblem(problem);
            _host.Post(() => OnPropertyChanged(day.ToString()));
            return;
        }

        var flag = BreakTimeRules.ToDays(day);
        _days = isSelected ? _days | flag : _days & ~flag;
        OnPropertyChanged(day.ToString());
        Send(day.ToString(), model => model.SetDayAsync(EntryId, day, isSelected));
    }

    private void CommitTime(BreakTimeBoundary boundary)
    {
        var isStart = boundary == BreakTimeBoundary.Start;
        if (!(isStart ? _startDirty : _endDirty))
        {
            return;
        }

        var name = isStart ? nameof(StartText) : nameof(EndText);
        if (isStart)
        {
            _startDirty = false;
        }
        else
        {
            _endDirty = false;
        }

        var problem = BreakTimeEditRules.CheckTime(_confirmed, boundary, isStart ? _startText : _endText, out var minute);
        var unchanged = minute == (isStart ? _confirmed.StartMinute : _confirmed.EndMinute);
        if (problem != EditProblem.None)
        {
            _host.ShowProblem(problem);
            _host.Post(Render);
            return;
        }

        SetTimeText(isStart, TimeOfDayText.Format(minute));
        if (!unchanged)
        {
            Send(name, model => model.SetTimeAsync(EntryId, boundary, minute));
        }
    }

    private void SetTimeText(bool isStart, string text)
    {
        if (isStart)
        {
            SetProperty(ref _startText, text, nameof(StartText));
        }
        else
        {
            SetProperty(ref _endText, text, nameof(EndText));
        }
    }

    private void Send(string field, Func<IAccountRulesModel, Task<bool>> write)
    {
        _pending[field] = _pending.GetValueOrDefault(field) + 1;
        LastWrite = SendAsync(field, write);
    }

    private async Task SendAsync(string field, Func<IAccountRulesModel, Task<bool>> write)
    {
        await _host.SendAsync(write);
        _host.Post(() =>
        {
            if (--_pending[field] == 0)
            {
                _pending.Remove(field);
            }

            Render();
        });
    }
}
