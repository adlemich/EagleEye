using EagleEye.Shared.Models;

namespace EagleEye.Shared.Constants;

/// <summary>Rules of the remaining-time overlay shared by the service and the tray client (US-010, ADR-022).</summary>
public static class OverlayRules
{
    /// <summary>The largest display threshold (FR-TRAY-031); the service sends lines up to this many minutes.</summary>
    public const int MaxThresholdMinutes = 60;

    /// <summary>Seconds per minute (level boundaries are whole minutes).</summary>
    private const int SecondsPerMinute = 60;

    /// <summary>
    /// The level of a line with <paramref name="seconds"/> left: Expired at 0 (or less), Final at ≤ final × 60, Early at
    /// ≤ early × 60, else Normal (same rule as the engine's warning levels, US-010 AC-7).
    /// </summary>
    public static OverlayLevel LevelFor(int seconds, int earlyMinutes, int finalMinutes)
    {
        if (seconds <= 0)
        {
            return OverlayLevel.Expired;
        }

        if (seconds <= finalMinutes * SecondsPerMinute)
        {
            return OverlayLevel.Final;
        }

        return seconds <= earlyMinutes * SecondsPerMinute ? OverlayLevel.Early : OverlayLevel.Normal;
    }
}
