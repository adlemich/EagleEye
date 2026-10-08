using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// One day on the Reports page (US-004 AC-18, AC-19): heading ("Heute, 07.10.2026" / "06.10.2026"), and either the
/// table of apps (longest usage first, equal usage by name) or, for today without usage, "Heute keine Nutzung
/// aufgezeichnet". Rows are merged by app (instances are kept).
/// </summary>
public sealed class DayUsageViewModel : ObservableObject
{
    private bool _isToday;

    /// <summary>Creates the day.</summary>
    public DayUsageViewModel(DateOnly day, bool isToday)
    {
        Day = day;
        _isToday = isToday;
    }

    /// <summary>The service PC's local date.</summary>
    public DateOnly Day { get; }

    /// <summary>The rows, sorted.</summary>
    public ObservableCollection<AppUsageRowViewModel> Rows { get; } = [];

    /// <summary>Whether the day is the service PC's today.</summary>
    public bool IsToday
    {
        get => _isToday;
        private set
        {
            if (SetProperty(ref _isToday, value))
            {
                OnPropertyChanged(nameof(Heading));
                OnPropertyChanged(nameof(ShowNoUsageToday));
            }
        }
    }

    /// <summary>The heading in the user's short date format.</summary>
    public string Heading
    {
        get
        {
            var date = Day.ToString("d", CultureInfo.CurrentCulture);
            return IsToday ? AppTexts.Format(AppTexts.ReportsTodayFormat, date) : date;
        }
    }

    /// <summary>The table (with its header) is shown.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>"No usage recorded today" is shown (today without rows).</summary>
    public bool ShowNoUsageToday => IsToday && Rows.Count == 0;

    /// <summary>Merges the apps of a newer snapshot and sorts the rows.</summary>
    internal void Update(IReadOnlyList<AppUsageDto> apps, bool isToday)
    {
        ArgumentNullException.ThrowIfNull(apps);
        IsToday = isToday;
        var hadRows = HasRows;
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var ordered = apps.OrderByDescending(a => a.Seconds).ThenBy(a => a.DisplayName, comparer).ToList();
        var keep = ordered.Select(a => a.AppId).ToHashSet();
        for (var index = Rows.Count - 1; index >= 0; index--)
        {
            if (!keep.Contains(Rows[index].AppId))
            {
                Rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            var app = ordered[index];
            var row = FindRow(app.AppId);
            if (row is null)
            {
                Rows.Insert(index, new AppUsageRowViewModel(app.AppId, app.DisplayName, app.Seconds));
                continue;
            }

            row.Update(app.DisplayName, app.Seconds);
            var current = Rows.IndexOf(row);
            if (current != index)
            {
                Rows.Move(current, index);
            }
        }

        if (hadRows != HasRows)
        {
            OnPropertyChanged(nameof(HasRows));
        }

        OnPropertyChanged(nameof(ShowNoUsageToday));
    }

    private AppUsageRowViewModel? FindRow(long appId)
    {
        foreach (var row in Rows)
        {
            if (row.AppId == appId)
            {
                return row;
            }
        }

        return null;
    }
}
