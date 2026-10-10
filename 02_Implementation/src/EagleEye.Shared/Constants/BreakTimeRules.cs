using System.Globalization;
using EagleEye.Shared.Models;

namespace EagleEye.Shared.Constants;

/// <summary>
/// Break-time rules shared by the service and the parent app (US-005, TC-010 to TC-014, FR-APP-052).
/// Times are minutes after midnight; 1439 (23:59) as an end means midnight (TC-011).
/// </summary>
public static class BreakTimeRules
{
    /// <summary>Maximum number of entries per account (OQ-8).</summary>
    public const int MaxEntriesPerAccount = 20;

    /// <summary>Maximum length of the display text in UTF-16 code units (OQ-8, plan D-8).</summary>
    public const int MaxDisplayTextLength = 500;

    /// <summary>The last minute of a day (23:59); as an end time it means midnight.</summary>
    public const int LastMinute = 1439;

    /// <summary>Start of a new entry: 20:00 (AC-7).</summary>
    public const int DefaultStartMinute = 1200;

    /// <summary>End of a new entry: 23:59 (AC-7).</summary>
    public const int DefaultEndMinute = LastMinute;

    /// <summary>Days of a new entry: all seven (AC-7).</summary>
    public const BreakTimeDays DefaultDays = BreakTimeDays.All;

    /// <summary>The default display text (AC-15), German in every language (OQ-14).</summary>
    public const string DefaultDisplayText =
        "Hi! Leider haben Deine Eltern eine PC-Pause für diese Uhrzeit eingestellt. Du kannst dieses Programm " +
        "jetzt nicht verwenden. Tut mir leid. Wie wäre es wenn Du die Zeit nutzt, um ein Buch zu lesen? 😊";

    private const int MinutesPerHour = 60;

    /// <summary>The day flag of a <see cref="DayOfWeek"/>.</summary>
    public static BreakTimeDays ToDays(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => BreakTimeDays.Monday,
            DayOfWeek.Tuesday => BreakTimeDays.Tuesday,
            DayOfWeek.Wednesday => BreakTimeDays.Wednesday,
            DayOfWeek.Thursday => BreakTimeDays.Thursday,
            DayOfWeek.Friday => BreakTimeDays.Friday,
            DayOfWeek.Saturday => BreakTimeDays.Saturday,
            DayOfWeek.Sunday => BreakTimeDays.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown weekday."),
        };
    }

    /// <summary>Replaces "\r\n" and "\r" by "\n" (WinUI returns "\r", TI-12 f).</summary>
    public static string NormalizeLineBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    /// <summary>
    /// Whether <paramref name="text"/> may be stored as a display text: at most
    /// <see cref="MaxDisplayTextLength"/> code units, no lone surrogates, no control characters except
    /// "\n" and "\t". Emojis, zero-width joiners and variation selectors are allowed (TI-14).
    /// </summary>
    public static bool IsValidDisplayText(string? text)
    {
        if (text is null || text.Length > MaxDisplayTextLength)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(c) || (char.IsControl(c) && c is not '\n' and not '\t'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a start and an end minute form a valid entry (TC-011): both within the day, end later than start.</summary>
    public static bool IsValidTimes(int startMinute, int endMinute)
    {
        return IsValidMinute(startMinute) && IsValidMinute(endMinute) && endMinute > startMinute;
    }

    /// <summary>Whether <paramref name="minute"/> is a time of day from 00:00 (0) to 23:59 (1439).</summary>
    public static bool IsValidMinute(int minute)
    {
        return minute is >= 0 and <= LastMinute;
    }

    /// <summary>Formats minutes after midnight as "HH:MM" (AC-3).</summary>
    public static string FormatMinute(int minute)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, LastMinute);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minute / MinutesPerHour:00}:{minute % MinutesPerHour:00}");
    }
}
