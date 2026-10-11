using System.Globalization;
using EagleEye.Service.Data;

namespace EagleEye.Service.Enforcement;

/// <summary>A controlled account's session that is in use (active, unlocked) at a tick.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="UserName">The account's user name.</param>
public sealed record SessionInUse(int SessionId, string AccountSid, string UserName);

/// <summary>
/// Finds time-zone and clock changes (ADR-013 §9, story AC-37, FR-SVC-026). Called at every 5 s tick after
/// <c>TimeZoneInfo.ClearCachedData()</c>. A zone change is a change of <c>Id</c>, <c>BaseUtcOffset</c> or
/// <c>SupportsDaylightSavingTime</c> (regular DST transitions change none of them). A clock jump is a difference of
/// more than <see cref="ClockJumpThreshold"/> between elapsed wall-clock and monotonic time; intervals longer than
/// <see cref="MaxJudgedInterval"/> and intervals around a suspend/resume (<see cref="Rebaseline"/>) are not judged.
/// The first tick only records the baseline (D-15). Each finding: Warning + one row in <c>TimeChangeFindings</c>, with
/// the controlled session in use if exactly one (a hint, not proof). Used by the accounting loop only.
/// </summary>
public sealed class TimeChangeMonitor(ITimeChangeFindingRepository repository, TimeProvider timeProvider, ILogger<TimeChangeMonitor> logger)
{
    /// <summary>Clock differences up to this are ignored (time synchronization).</summary>
    public static readonly TimeSpan ClockJumpThreshold = TimeSpan.FromSeconds(30);

    /// <summary>Longer intervals between two ticks are not judged (sleep, hangs; ADR-012 §3).</summary>
    public static readonly TimeSpan MaxJudgedInterval = TimeSpan.FromSeconds(15);

    /// <summary>Kind of a zone finding.</summary>
    public const string TimeZoneKind = "TimeZone";

    /// <summary>Kind of a clock finding.</summary>
    public const string ClockKind = "Clock";

    private const string LocalFormat = "yyyy-MM-dd HH:mm:ss";

    private (string Id, TimeSpan Offset, bool Dst)? _zone;
    private DateTimeOffset _lastWall;
    private long _lastTimestamp;
    private bool _clockBaseline;

    /// <summary>Checks the zone and the clock against the last tick and stores findings.</summary>
    public async Task OnTickAsync(IReadOnlyList<SessionInUse> sessionsInUse, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionsInUse);
        var who = sessionsInUse.Count == 1 ? sessionsInUse[0] : null;
        var zone = timeProvider.LocalTimeZone;
        var wallNow = timeProvider.GetUtcNow();
        var timestamp = timeProvider.GetTimestamp();

        var current = (zone.Id, zone.BaseUtcOffset, zone.SupportsDaylightSavingTime);
        if (_zone is { } previous && previous != current)
        {
            await ReportAsync(TimeZoneKind, Describe(previous), Describe(current), who, wallNow, zone, ct).ConfigureAwait(false);
        }

        _zone = current;
        if (_clockBaseline)
        {
            var monotonic = timeProvider.GetElapsedTime(_lastTimestamp, timestamp);
            var expected = _lastWall + monotonic;
            if (monotonic <= MaxJudgedInterval && (wallNow - expected).Duration() > ClockJumpThreshold)
            {
                await ReportAsync(ClockKind, Local(expected, zone), Local(wallNow, zone), who, wallNow, zone, ct).ConfigureAwait(false);
            }
        }

        _lastWall = wallNow;
        _lastTimestamp = timestamp;
        _clockBaseline = true;
    }

    /// <summary>The PC suspended or resumed: the next tick only records a new clock baseline.</summary>
    public void Rebaseline() => _clockBaseline = false;

    private async Task ReportAsync(
        string kind, string oldValue, string newValue, SessionInUse? who, DateTimeOffset wallNow, TimeZoneInfo zone, CancellationToken ct)
    {
        var what = kind == TimeZoneKind ? "time zone" : "clock";
        if (who is null)
        {
            logger.LogWarning("The {What} of the PC changed from {Old} to {New}; break times now use the new local time.", what, oldValue, newValue);
        }
        else
        {
            logger.LogWarning(
                "The {What} of the PC changed from {Old} to {New}; break times now use the new local time. Session in use: {UserName}.",
                what, oldValue, newValue, who.UserName);
        }

        var finding = new TimeChangeFinding(
            wallNow, TimeZoneInfo.ConvertTime(wallNow, zone).DateTime, kind, oldValue, newValue, who?.SessionId, who?.AccountSid, who?.UserName);
        try
        {
            await repository.InsertAsync(finding, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Storage boundary: the Warning in the log is kept; the next finding is stored normally.
            logger.LogWarning(ex, "Storing a {Kind} finding failed.", kind);
        }
    }

    private static string Describe((string Id, TimeSpan Offset, bool Dst) zone)
    {
        var sign = zone.Offset < TimeSpan.Zero ? "-" : "+";
        var offset = zone.Offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        return $"{zone.Id} (UTC{sign}{offset}{(zone.Dst ? string.Empty : ", no DST")})";
    }

    private static string Local(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString(LocalFormat, CultureInfo.InvariantCulture);
}
