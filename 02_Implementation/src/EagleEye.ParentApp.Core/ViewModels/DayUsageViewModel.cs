using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// One day on the Reports page (US-004 AC-18, AC-19): heading ("Heute, 07.10.2026" / "06.10.2026"), and either the
/// table of apps (longest usage first, equal usage by name) or, for today without usage, "Heute keine Nutzung
/// aufgezeichnet". Rows are merged by app (instances are kept). US-008 AC-35: below, the group lines, sorted the same way.
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

    /// <summary>The group lines (US-008 AC-35), sorted like the rows; <see cref="AppUsageRowViewModel.AppId"/> is the group id.</summary>
    public ObservableCollection<AppUsageRowViewModel> GroupRows { get; } = [];

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

    /// <summary>"App groups" and its table are shown: at least one group has used time on the day (AC-35).</summary>
    public bool HasGroupRows => GroupRows.Count > 0;

    /// <summary>"No usage recorded today" is shown (today without rows).</summary>
    public bool ShowNoUsageToday => IsToday && Rows.Count == 0;

    /// <summary>Merges the apps and groups of a newer snapshot and sorts the rows.</summary>
    internal void Update(IReadOnlyList<AppUsageDto> apps, bool isToday, IReadOnlyList<GroupUsageDto>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(apps);
        IsToday = isToday;
        var hadRows = HasRows;
        var hadGroupRows = HasGroupRows;
        Merge(Rows, apps.Select(a => (a.AppId, a.DisplayName, a.Seconds)));
        Merge(GroupRows, (groups ?? []).Select(g => (g.GroupId, g.Name, g.Seconds)));
        if (hadRows != HasRows)
        {
            OnPropertyChanged(nameof(HasRows));
        }

        if (hadGroupRows != HasGroupRows)
        {
            OnPropertyChanged(nameof(HasGroupRows));
        }

        OnPropertyChanged(nameof(ShowNoUsageToday));
    }

    /// <summary>Longest usage first, equal usage by name.</summary>
    private static void Merge(ObservableCollection<AppUsageRowViewModel> rows, IEnumerable<(long Id, string Name, long Seconds)> items)
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var ordered = items.OrderByDescending(i => i.Seconds).ThenBy(i => i.Name, comparer).ToList();
        OrderedMerge.Apply(rows, ordered, row => row.AppId, item => item.Id, item => new AppUsageRowViewModel(item.Id, item.Name, item.Seconds), (row, item) => row.Update(item.Name, item.Seconds));
    }
}
