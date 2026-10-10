using CommunityToolkit.Mvvm.ComponentModel;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>One row of a day's table on the Reports page (US-004 AC-18): app name and usage as HH:MM.</summary>
public sealed class AppUsageRowViewModel : ObservableObject
{
    private string _displayName;
    private long _seconds;

    /// <summary>Creates a row.</summary>
    public AppUsageRowViewModel(long appId, string displayName, long seconds)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        AppId = appId;
        _displayName = displayName;
        _seconds = seconds;
    }

    /// <summary>The app record (stable key of the row).</summary>
    public long AppId { get; }

    /// <summary>The app's display name (AC-8).</summary>
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    /// <summary>Active seconds on the day.</summary>
    public long Seconds
    {
        get => _seconds;
        private set
        {
            if (SetProperty(ref _seconds, value))
            {
                OnPropertyChanged(nameof(UsageText));
            }
        }
    }

    /// <summary>The usage as HH:MM.</summary>
    public string UsageText => UsageDuration.ToHhMm(Seconds);

    /// <summary>Shows a newer value of the service.</summary>
    internal void Update(string displayName, long seconds)
    {
        DisplayName = displayName;
        Seconds = seconds;
    }
}
