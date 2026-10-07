namespace EagleEye.Shared.Communication;

/// <summary>
/// Reconnect timing for a lost connection: retries after 0 s, 2 s and 10 s, then every 30 s
/// forever. The SignalR default policy gives up after four attempts, but EagleEye clients must
/// keep trying for as long as they run (FR-TRAY-042, US-002 AC-22).
/// </summary>
public static class ReconnectSchedule
{
    private static readonly TimeSpan[] InitialDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
    ];

    private static readonly TimeSpan SteadyDelay = TimeSpan.FromSeconds(30);

    /// <summary>Returns the delay before the next reconnect attempt.</summary>
    /// <param name="previousRetryCount">Number of reconnect attempts made so far.</param>
    public static TimeSpan GetDelay(long previousRetryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previousRetryCount);

        return previousRetryCount < InitialDelays.Length
            ? InitialDelays[previousRetryCount]
            : SteadyDelay;
    }
}
