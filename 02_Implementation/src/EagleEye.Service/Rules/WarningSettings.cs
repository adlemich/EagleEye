using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>The two global time-elapsed warnings (US-006 AC-13 to AC-15, ADR-015 §4; minutes only since US-010).</summary>
/// <param name="EarlyMinutes">Minutes before the end of the early warning.</param>
/// <param name="FinalMinutes">Minutes before the end of the final warning.</param>
public sealed record WarningSettings(int EarlyMinutes, int FinalMinutes)
{
    /// <summary>The defaults: 5 and 1 minutes (AC-14).</summary>
    public static WarningSettings Defaults { get; } = new(WarningRules.DefaultEarlyMinutes, WarningRules.DefaultFinalMinutes);

    /// <summary>The minutes of a level.</summary>
    public int MinutesOf(WarningLevel level) => level == WarningLevel.Early ? EarlyMinutes : FinalMinutes;

    /// <summary>A copy with the minutes of a level changed.</summary>
    public WarningSettings WithMinutes(WarningLevel level, int minutes) =>
        level == WarningLevel.Early ? this with { EarlyMinutes = minutes } : this with { FinalMinutes = minutes };
}
