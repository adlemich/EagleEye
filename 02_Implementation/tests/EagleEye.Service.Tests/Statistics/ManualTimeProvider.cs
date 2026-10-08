namespace EagleEye.Service.Tests.Statistics;

/// <summary>A clock whose monotonic time and wall time can be moved independently (clock-change tests).</summary>
internal sealed class ManualTimeProvider(DateTimeOffset wallNow, TimeZoneInfo zone) : TimeProvider
{
    private long _timestamp = 1_000_000;

    public DateTimeOffset WallNow { get; set; } = wallNow;

    public override TimeZoneInfo LocalTimeZone => zone;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => WallNow;

    public override long GetTimestamp() => _timestamp;

    /// <summary>Moves both clocks forward (normal passing of time).</summary>
    public void Advance(TimeSpan time)
    {
        _timestamp += time.Ticks;
        WallNow += time;
    }

    /// <summary>Moves only the monotonic clock (e.g. the wall clock is set back meanwhile).</summary>
    public void AdvanceMonotonic(TimeSpan time) => _timestamp += time.Ticks;
}
