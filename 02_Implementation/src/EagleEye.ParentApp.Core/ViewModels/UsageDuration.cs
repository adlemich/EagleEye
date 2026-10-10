using System.Globalization;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>Usage durations as HH:MM, rounded down like a clock (US-004 AC-19, OQ-4).</summary>
public static class UsageDuration
{
    /// <summary>"00:00" for 59 s, "00:59" for 3 599 s, "24:00" for 86 400 s; negative values give "00:00".</summary>
    public static string ToHhMm(long seconds)
    {
        var minutes = Math.Max(0, seconds) / 60;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes / 60:00}:{minutes % 60:00}");
    }
}
