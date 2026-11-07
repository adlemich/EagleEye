using System.Globalization;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.TrayClient.UI;

namespace EagleEye.TrayClient.Overlay;

/// <summary>One line of the pill as drawn.</summary>
/// <param name="Text">"Editor 4:12", "Break in 18:30" or "+3 more".</param>
/// <param name="Level">Its colour (the "+N" line is <see cref="OverlayLevel.Normal"/>).</param>
/// <param name="IsTop">The first line: large font (AC-4).</param>
internal sealed record PillLine(string Text, OverlayLevel Level, bool IsTop);

/// <summary>What the pill shows; no lines = overlay hidden.</summary>
/// <param name="Lines">The lines, top line first.</param>
/// <param name="AnyFinalAboveZero">Whether a shown line is red and above 0:00 (slow pulse, AC-8).</param>
internal sealed record PillLayout(IReadOnlyList<PillLine> Lines, bool AnyFinalAboveZero);

/// <summary>
/// Which lines the pill shows and how (US-010 Decision 5, AC-3, AC-4): a line is <b>due</b> while its displayed seconds are
/// below the display threshold, or at or below the early level; due lines are sorted by seconds (ties: the break line first,
/// then names ignoring case in the UI culture, then ordinal); at most 5 lines, with more the fifth reads "+N more"; names
/// longer than 24 text elements are shortened with "…"; times are m:ss below one hour, h:mm:ss from one hour. Pure.
/// </summary>
internal static class OverlayLayout
{
    /// <summary>The most lines the pill shows.</summary>
    public const int MaxLines = 5;

    /// <summary>The longest name in text elements; longer names are cut to one less plus "…".</summary>
    public const int MaxNameLength = 24;

    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const string Ellipsis = "…";

    /// <summary>The layout of <paramref name="lines"/>.</summary>
    /// <param name="lines">The current lines with their displayed seconds.</param>
    /// <param name="showFromMinutes">The display threshold.</param>
    /// <param name="earlyMinutes">Early-warning minutes.</param>
    /// <param name="finalMinutes">Final-warning minutes.</param>
    /// <param name="culture">The UI culture (texts, name order).</param>
    public static PillLayout Build(IReadOnlyList<DisplayedLine> lines, int showFromMinutes, int earlyMinutes, int finalMinutes, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(culture);
        var names = StringComparer.Create(culture, ignoreCase: true);
        var due = lines
            .Select(l => (Line: l, Level: OverlayRules.LevelFor(l.Seconds, earlyMinutes, finalMinutes)))
            .Where(l => l.Line.Seconds < showFromMinutes * SecondsPerMinute || l.Level >= OverlayLevel.Early)
            .OrderBy(l => l.Line.Seconds)
            .ThenBy(l => l.Line.Kind == OverlayLineKind.Break ? 0 : 1)
            .ThenBy(l => l.Line.Name, names)
            .ThenBy(l => l.Line.Name, StringComparer.Ordinal)
            .ToList();
        var shown = due.Count > MaxLines ? due.Take(MaxLines - 1).ToList() : due;
        var result = shown.Select((l, i) => new PillLine(Text(l.Line, culture), l.Level, i == 0)).ToList();
        if (due.Count > MaxLines)
        {
            result.Add(new PillLine(string.Format(culture, TrayTexts.Get("MoreLinesFormat", culture), due.Count - shown.Count), OverlayLevel.Normal, false));
        }

        return new PillLayout(result, shown.Any(l => l.Level == OverlayLevel.Final));
    }

    /// <summary>"m:ss" below one hour, "h:mm:ss" from one hour; never below 0:00.</summary>
    public static string FormatTime(int seconds)
    {
        var value = Math.Max(0, seconds);
        return value < SecondsPerHour
            ? string.Create(CultureInfo.InvariantCulture, $"{value / SecondsPerMinute}:{value % SecondsPerMinute:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{value / SecondsPerHour}:{value % SecondsPerHour / SecondsPerMinute:00}:{value % SecondsPerMinute:00}");
    }

    /// <summary>The name, cut to <see cref="MaxNameLength"/> text elements (an emoji is never cut in half).</summary>
    public static string ShortName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var info = new StringInfo(name);
        return info.LengthInTextElements > MaxNameLength ? info.SubstringByTextElements(0, MaxNameLength - 1) + Ellipsis : name;
    }

    private static string Text(DisplayedLine line, CultureInfo culture)
    {
        var time = FormatTime(line.Seconds);
        return line.Kind == OverlayLineKind.Break
            ? string.Format(culture, TrayTexts.Get("BreakLineFormat", culture), time)
            : $"{ShortName(line.Name ?? string.Empty)} {time}";
    }
}
