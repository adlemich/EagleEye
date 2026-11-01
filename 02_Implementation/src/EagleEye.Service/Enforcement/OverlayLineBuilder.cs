using EagleEye.Service.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Builds the lines of the remaining-time overlay (US-010 Decision 2, ADR-015 as amended by US-010). App and group lines show
/// the subject's <b>own</b> remaining time (budget, not allowed today, midnight; no break times), or the grace left during an
/// app grace period; the break line shows the time until the next break begins, or the grace left during a break grace
/// period. Only times within <see cref="AppEndCalculator.Horizon"/> give a line. Display only: the warnings stay on the
/// combined end of the engine. Pure.
/// </summary>
public static class OverlayLineBuilder
{
    /// <summary>The line of a running subject, or <c>null</c> when its own end is beyond the horizon.</summary>
    /// <param name="subject">The subject.</param>
    /// <param name="appGraceLeft">Seconds left of an app grace period of the subject, or <c>null</c>.</param>
    /// <param name="sessionInUse">Whether the session is in use (a budget counts down only then).</param>
    /// <param name="settings">The warning levels.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="zone">The local zone.</param>
    public static OverlayLineDto? SubjectLine(
        LimitSubject subject, double? appGraceLeft, bool sessionInUse, WarningSettings settings, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(settings);
        var kind = subject.GroupId is null ? OverlayLineKind.App : OverlayLineKind.Group;
        if (appGraceLeft is { } grace)
        {
            return Line(kind, subject.Name, grace, true, settings);
        }

        var own = AppEndCalculator.Compute(subject.Rule, subject.UsedSeconds, [], nowUtc, zone);
        return own is null ? null : Line(kind, subject.Name, own.SecondsLeft, own.Kind != AppEndKind.Budget || sessionInUse, settings);
    }

    /// <summary>The break line, or <c>null</c> when no break begins within the horizon and no break grace period runs.</summary>
    /// <param name="entries">The account's break entries.</param>
    /// <param name="breakGraceLeft">The smallest grace left of a break grace period in the session, or <c>null</c>.</param>
    /// <param name="settings">The warning levels.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="zone">The local zone.</param>
    public static OverlayLineDto? BreakLine(
        IReadOnlyList<BreakTimeEntryDto> entries, double? breakGraceLeft, WarningSettings settings, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(zone);
        var seconds = breakGraceLeft
            ?? (BreakTimeSchedule.FirstInEffect(entries, BreakTimeSchedule.ToLocal(nowUtc, zone)) is not null
                ? 0
                : BreakTimeSchedule.NextBegin(entries, nowUtc, zone, AppEndCalculator.Horizon)?.Until.TotalSeconds);
        return seconds is { } value ? Line(OverlayLineKind.Break, null, value, true, settings) : null;
    }

    /// <summary>Seconds for the overlay: rounded up, never below 0.</summary>
    public static int Seconds(double seconds) => Math.Max(0, (int)Math.Ceiling(seconds));

    private static OverlayLineDto Line(OverlayLineKind kind, string? name, double secondsLeft, bool counting, WarningSettings settings)
    {
        var seconds = Seconds(secondsLeft);
        return new OverlayLineDto(kind, name, seconds, OverlayRules.LevelFor(seconds, settings.EarlyMinutes, settings.FinalMinutes), counting);
    }
}
