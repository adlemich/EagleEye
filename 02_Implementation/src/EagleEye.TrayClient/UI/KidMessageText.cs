using System.Globalization;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.Shared.Resources;

namespace EagleEye.TrayClient.UI;

/// <summary>What the kid dialog shows: the parent's text (or its default) and EagleEye's info line below it.</summary>
/// <param name="Text">The parent's text or the default text in the kid's language; line breaks "\n".</param>
/// <param name="Info">The info line naming the reason (US-006 AC-34).</param>
internal sealed record KidMessageContent(string Text, string Info);

/// <summary>
/// Renders a kid message in the kid's language (US-006 Decision 9, ADR-016): the parent's text, or the default text of
/// the reason's kind in the UI culture, and the info line by reason (singular or plural minutes, times in the kid's
/// short time format; an end of 23:59 is midnight). Pure.
/// </summary>
internal static class KidMessageText
{
    /// <summary>The content of the message.</summary>
    /// <param name="message">The service's message.</param>
    /// <param name="uiCulture">The language of the texts.</param>
    /// <param name="formatCulture">The format of the times.</param>
    public static KidMessageContent Compose(KidMessageDto message, CultureInfo uiCulture, CultureInfo formatCulture)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(uiCulture);
        ArgumentNullException.ThrowIfNull(formatCulture);
        var text = message.ParentText ?? DefaultTexts.Get(KindOf(message.Reason), uiCulture);
        var minutes = message.Minutes ?? 1;
        var info = string.Format(
            formatCulture,
            TrayTexts.Get(InfoKey(message.Reason, minutes), uiCulture),
            message.AppName ?? string.Empty,
            minutes,
            Time(message.BreakStartMinute, uiCulture, formatCulture),
            Time(message.BreakEndMinute, uiCulture, formatCulture));
        return new KidMessageContent(text, info);
    }

    /// <summary>The default text of a reason (ADR-016).</summary>
    internal static DefaultTextKind KindOf(KidMessageReason reason) => reason switch
    {
        KidMessageReason.AppEarlyWarning or KidMessageReason.BreakEarlyWarning => DefaultTextKind.EarlyWarningText,
        KidMessageReason.AppFinalWarning or KidMessageReason.BreakFinalWarning
            or KidMessageReason.AppGracePeriod or KidMessageReason.BreakGracePeriod => DefaultTextKind.FinalWarningText,
        KidMessageReason.AppNotAllowedToday or KidMessageReason.AppBudgetUsedUp => DefaultTextKind.BudgetText,
        _ => DefaultTextKind.BreakDisplayText,
    };

    private static string InfoKey(KidMessageReason reason, int minutes) => reason switch
    {
        KidMessageReason.AppEarlyWarning or KidMessageReason.AppFinalWarning => minutes == 1 ? "InfoMinutesLeftOne" : "InfoMinutesLeftMany",
        KidMessageReason.BreakEarlyWarning or KidMessageReason.BreakFinalWarning => "InfoBreakBegins",
        KidMessageReason.AppGracePeriod => minutes == 1 ? "InfoAppGraceOne" : "InfoAppGraceMany",
        KidMessageReason.BreakGracePeriod => minutes == 1 ? "InfoBreakGraceOne" : "InfoBreakGraceMany",
        KidMessageReason.AppNotAllowedToday => "InfoNotAllowedToday",
        KidMessageReason.AppBudgetUsedUp => "InfoBudgetUsedUp",
        _ => "InfoBreakTime",
    };

    /// <summary>A time of day in the kid's short time format; 23:59 as an end means midnight.</summary>
    private static string Time(int? minute, CultureInfo uiCulture, CultureInfo formatCulture)
    {
        if (minute is not { } value)
        {
            return string.Empty;
        }

        return value == BreakTimeRules.LastMinute
            ? TrayTexts.Get("InfoMidnight", uiCulture)
            : DateTime.MinValue.AddMinutes(value).ToString("t", formatCulture);
    }
}
