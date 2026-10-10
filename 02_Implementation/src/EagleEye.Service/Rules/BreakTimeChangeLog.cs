using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// Pure texts for the service log (English, arc42 §8.13): descriptions of rules changes (AC-12) and of entries
/// ("20:00–23:59 (Mo Tu We Th Fr)"). The display text itself is never logged.
/// </summary>
public static class BreakTimeChangeLog
{
    private static readonly (BreakTimeDays Day, string Text)[] DayTexts =
    [
        (BreakTimeDays.Monday, "Mo"), (BreakTimeDays.Tuesday, "Tu"), (BreakTimeDays.Wednesday, "We"), (BreakTimeDays.Thursday, "Th"),
        (BreakTimeDays.Friday, "Fr"), (BreakTimeDays.Saturday, "Sa"), (BreakTimeDays.Sunday, "Su"),
    ];

    /// <summary>"entry added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su)".</summary>
    public static string Added(BreakTimeEntryDto entry) => $"entry {Id(entry)} added ({Values(entry)})";

    /// <summary>"entry 3 changed: on, 20:00–23:59, Mo Tu We Th Fr".</summary>
    public static string Changed(BreakTimeEntryDto entry) => $"entry {Id(entry)} changed: {Values(entry)}";

    /// <summary>"entry 3 deleted".</summary>
    public static string Deleted(long entryId) => $"entry {entryId} deleted";

    /// <summary>"display text changed".</summary>
    public static string TextChanged() => "display text changed";

    /// <summary>"display text reset to the default".</summary>
    public static string TextReset() => "display text reset to the default";

    /// <summary>"20:00–23:59".</summary>
    public static string Range(BreakTimeEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return $"{BreakTimeRules.FormatMinute(entry.StartMinute)}–{BreakTimeRules.FormatMinute(entry.EndMinute)}";
    }

    /// <summary>"Mo Tu We" (Monday first).</summary>
    public static string Days(BreakTimeDays days) =>
        string.Join(' ', DayTexts.Where(d => (days & d.Day) != BreakTimeDays.None).Select(d => d.Text));

    /// <summary>The abbreviation of a weekday, e.g. "Mo".</summary>
    public static string Weekday(DayOfWeek day) => Days(BreakTimeRules.ToDays(day));

    private static string Id(BreakTimeEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Values(BreakTimeEntryDto entry) =>
        $"{(entry.IsActive ? "on" : "off")}, {Range(entry)}, {Days(entry.Days)}";
}
