namespace EagleEye.TrayClient.Communication;

/// <summary>
/// Delay between initial connection attempts while the service is not reachable:
/// exponential (1 s, 2 s, 4 s, ...), capped at 30 s.
/// </summary>
internal static class ConnectBackoff
{
    private const int MaxDelaySeconds = 30;

    /// <summary>Returns the delay after the failed attempt with the given zero-based index.</summary>
    public static TimeSpan GetDelay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);

        // 2^5 = 32 already exceeds the cap; avoids overflow for large attempt counts.
        var seconds = attempt >= 5 ? MaxDelaySeconds : Math.Min(1 << attempt, MaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
