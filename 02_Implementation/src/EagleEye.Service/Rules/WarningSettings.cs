using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>The two global time-elapsed warnings (US-006 AC-13 to AC-15, ADR-015 §4). Texts <c>null</c> = default (ADR-016).</summary>
/// <param name="EarlyMinutes">Minutes before the end of the early warning.</param>
/// <param name="EarlyText">The parent's early warning text, or <c>null</c>.</param>
/// <param name="FinalMinutes">Minutes before the end of the final warning.</param>
/// <param name="FinalText">The parent's final warning text, or <c>null</c>.</param>
public sealed record WarningSettings(int EarlyMinutes, string? EarlyText, int FinalMinutes, string? FinalText)
{
    /// <summary>The defaults: 5 and 1 minutes with the default texts (AC-14).</summary>
    public static WarningSettings Defaults { get; } = new(WarningRules.DefaultEarlyMinutes, null, WarningRules.DefaultFinalMinutes, null);

    /// <summary>The minutes of a level.</summary>
    public int MinutesOf(WarningLevel level) => level == WarningLevel.Early ? EarlyMinutes : FinalMinutes;

    /// <summary>The parent's text of a level, or <c>null</c>.</summary>
    public string? TextOf(WarningLevel level) => level == WarningLevel.Early ? EarlyText : FinalText;

    /// <summary>A copy with the minutes of a level changed.</summary>
    public WarningSettings WithMinutes(WarningLevel level, int minutes) =>
        level == WarningLevel.Early ? this with { EarlyMinutes = minutes } : this with { FinalMinutes = minutes };

    /// <summary>A copy with the text of a level changed.</summary>
    public WarningSettings WithText(WarningLevel level, string? text) =>
        level == WarningLevel.Early ? this with { EarlyText = text } : this with { FinalText = text };
}
