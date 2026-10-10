using System.Diagnostics.CodeAnalysis;
using EagleEye.Shared.Constants;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Typed times of the break-time table (AC-9, plan TI-12 c): accepts <c>H</c>, <c>HH</c>, <c>H:MM</c>, <c>HH:MM</c>,
/// <c>HMM</c>, <c>HHMM</c>, with <c>:</c> or <c>.</c> as separator and surrounding white space, from 00:00 to 23:59
/// ("24:00" is invalid; 23:59 as an end means midnight). Always shown as <c>HH:MM</c>.
/// </summary>
public static class TimeOfDayText
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;

    /// <summary>Parses a typed time into minutes after midnight.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out int minute)
    {
        minute = 0;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.IndexOfAny([':', '.']);
        string hours;
        string minutes;
        if (separator >= 0)
        {
            hours = value[..separator];
            minutes = value[(separator + 1)..];
            if (minutes.Length != 2)
            {
                return false;
            }
        }
        else if (value.Length <= 2)
        {
            hours = value;
            minutes = "00";
        }
        else
        {
            hours = value[..^2];
            minutes = value[^2..];
        }

        if (hours.Length is 0 or > 2 || !hours.All(char.IsAsciiDigit) || !minutes.All(char.IsAsciiDigit))
        {
            return false;
        }

        var h = int.Parse(hours, System.Globalization.CultureInfo.InvariantCulture);
        var m = int.Parse(minutes, System.Globalization.CultureInfo.InvariantCulture);
        if (h >= HoursPerDay || m >= MinutesPerHour)
        {
            return false;
        }

        minute = (h * MinutesPerHour) + m;
        return true;
    }

    /// <summary>Formats minutes after midnight as <c>HH:MM</c> (AC-3).</summary>
    public static string Format(int minute) => BreakTimeRules.FormatMinute(minute);
}
