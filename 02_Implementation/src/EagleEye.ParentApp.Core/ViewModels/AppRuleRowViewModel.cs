using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>Which budget field was refused (US-006 AC-8).</summary>
public enum AppBudgetProblem
{
    /// <summary>Hours not 0 … 23.</summary>
    InvalidHours,

    /// <summary>Minutes not 0 … 59.</summary>
    InvalidMinutes,
}

/// <summary>What an app-rule row needs from its section (<see cref="AppRulesSectionViewModel"/>).</summary>
internal interface IAppRuleRowHost
{
    /// <summary>Queues an action on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Sends a write; shows the AC-12 message on failure and clears the message on success. Never throws.</summary>
    Task<bool> SendAsync(Func<IAppRulesModel, Task<bool>> write);

    /// <summary>Shows the message of a refused field (AC-8).</summary>
    void ShowProblem(AppBudgetProblem problem);
}

/// <summary>
/// One row of the application rules table (US-006 AC-2, AC-7, AC-8), edited in place. Weekday checkboxes are sent at the
/// click and show the requested value while the write is pending, the confirmed one afterwards (ADR-010 §5). The hours
/// and minutes fields are sent on <see cref="CommitHours"/>/<see cref="CommitMinutes"/> (leaving the field, Enter); an
/// invalid value returns to the confirmed value with a message. Snapshots (<see cref="Update"/>) never overwrite a field
/// with uncommitted typing or a pending write (AC-17). Rows are merged by <see cref="AppId"/>. UI thread only.
/// </summary>
public sealed class AppRuleRowViewModel : ObservableObject
{
    private const string Hours = "Hours";
    private const string Minutes = "Minutes";

    private readonly IAppRuleRowHost _host;
    private readonly Dictionary<string, int> _pending = [];
    private AppRuleDto _confirmed;
    private BreakTimeDays _days;
    private string _hoursText;
    private string _minutesText;
    private bool _hoursDirty;
    private bool _minutesDirty;

    internal AppRuleRowViewModel(AppRuleDto confirmed, IAppRuleRowHost host)
    {
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _days = confirmed.Days;
        _hoursText = AppBudgetText.Format(AppRuleRules.HoursOf(confirmed.BudgetMinutes));
        _minutesText = AppBudgetText.Format(AppRuleRules.MinutesOf(confirmed.BudgetMinutes));
    }

    /// <summary>The app record.</summary>
    public long AppId => _confirmed.AppId;

    /// <summary>The display name, as on Reports (AC-2).</summary>
    public string DisplayName => _confirmed.DisplayName;

    /// <summary>The program path, shown as a tooltip (OQ-12).</summary>
    public string ProgramPath => _confirmed.ProgramPath;

    /// <summary>The hours of the budget as typed or confirmed.</summary>
    public string HoursText
    {
        get => _hoursText;
        set
        {
            if (SetProperty(ref _hoursText, value ?? string.Empty))
            {
                _hoursDirty = true;
            }
        }
    }

    /// <summary>The minutes of the budget as typed or confirmed.</summary>
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

#pragma warning disable CS1591 // One checkbox per weekday (AC-2), Monday first.
    public bool Monday { get => Has(DayOfWeek.Monday); set => SetDay(DayOfWeek.Monday, value); }

    public bool Tuesday { get => Has(DayOfWeek.Tuesday); set => SetDay(DayOfWeek.Tuesday, value); }

    public bool Wednesday { get => Has(DayOfWeek.Wednesday); set => SetDay(DayOfWeek.Wednesday, value); }

    public bool Thursday { get => Has(DayOfWeek.Thursday); set => SetDay(DayOfWeek.Thursday, value); }

    public bool Friday { get => Has(DayOfWeek.Friday); set => SetDay(DayOfWeek.Friday, value); }

    public bool Saturday { get => Has(DayOfWeek.Saturday); set => SetDay(DayOfWeek.Saturday, value); }

    public bool Sunday { get => Has(DayOfWeek.Sunday); set => SetDay(DayOfWeek.Sunday, value); }
#pragma warning restore CS1591

    /// <summary>Whether a budget field has typing that was not committed yet.</summary>
    public bool HasUncommittedText => _hoursDirty || _minutesDirty;

    /// <summary>The last write started by this row (awaited by tests).</summary>
    internal Task LastWrite { get; private set; } = Task.CompletedTask;

    /// <summary>Commits changed hours (leaving the field or Enter, AC-7, AC-8).</summary>
    public void CommitHours() => Commit(AppBudgetPart.Hours);

    /// <summary>Commits changed minutes (leaving the field or Enter, AC-7, AC-8).</summary>
    public void CommitMinutes() => Commit(AppBudgetPart.Minutes);

    /// <summary>Applies the confirmed rule of a snapshot to every field without typing or a pending write.</summary>
    internal void Update(AppRuleDto confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        var renamed = confirmed.DisplayName != _confirmed.DisplayName || confirmed.ProgramPath != _confirmed.ProgramPath;
        _confirmed = confirmed;
        if (renamed)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ProgramPath));
        }

        Render();
    }

    private void Render()
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var flag = BreakTimeRules.ToDays(day);
            if (!_pending.ContainsKey(day.ToString()) && (_days & flag) != (_confirmed.Days & flag))
            {
                _days = (_days & ~flag) | (_confirmed.Days & flag);
                OnPropertyChanged(day.ToString());
            }
        }

        if (!_hoursDirty && !_pending.ContainsKey(Hours))
        {
            SetProperty(ref _hoursText, AppBudgetText.Format(AppRuleRules.HoursOf(_confirmed.BudgetMinutes)), nameof(HoursText));
        }

        if (!_minutesDirty && !_pending.ContainsKey(Minutes))
        {
            SetProperty(ref _minutesText, AppBudgetText.Format(AppRuleRules.MinutesOf(_confirmed.BudgetMinutes)), nameof(MinutesText));
        }
    }

    private bool Has(DayOfWeek day) => (_days & BreakTimeRules.ToDays(day)) != BreakTimeDays.None;

    private void SetDay(DayOfWeek day, bool isAllowed)
    {
        if (Has(day) == isAllowed)
        {
            return;
        }

        var flag = BreakTimeRules.ToDays(day);
        _days = isAllowed ? _days | flag : _days & ~flag;
        OnPropertyChanged(day.ToString());
        Send(day.ToString(), model => model.SetDayAsync(AppId, day, isAllowed));
    }

    private void Commit(AppBudgetPart part)
    {
        var isHours = part == AppBudgetPart.Hours;
        if (!(isHours ? _hoursDirty : _minutesDirty))
        {
            return;
        }

        if (isHours)
        {
            _hoursDirty = false;
        }
        else
        {
            _minutesDirty = false;
        }

        var valid = isHours ? AppBudgetText.TryParseHours(_hoursText, out var value) : AppBudgetText.TryParseMinutes(_minutesText, out value);
        if (!valid)
        {
            _host.ShowProblem(isHours ? AppBudgetProblem.InvalidHours : AppBudgetProblem.InvalidMinutes);
            _host.Post(Render);
            return;
        }

        var confirmed = isHours ? AppRuleRules.HoursOf(_confirmed.BudgetMinutes) : AppRuleRules.MinutesOf(_confirmed.BudgetMinutes);
        var text = AppBudgetText.Format(value);
        if (isHours)
        {
            SetProperty(ref _hoursText, text, nameof(HoursText));
        }
        else
        {
            SetProperty(ref _minutesText, text, nameof(MinutesText));
        }

        if (value != confirmed)
        {
            Send(isHours ? Hours : Minutes, model => model.SetBudgetAsync(AppId, part, value));
        }
    }

    private void Send(string field, Func<IAppRulesModel, Task<bool>> write)
    {
        _pending[field] = _pending.GetValueOrDefault(field) + 1;
        LastWrite = SendAsync(field, write);
    }

    private async Task SendAsync(string field, Func<IAppRulesModel, Task<bool>> write)
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
