using System.Globalization;

namespace EagleEye.Service.Statistics;

/// <summary>
/// The service-log entries of the app inventory and history (US-004 AC-7, AC-10), with the per-app hourly limit
/// of <see cref="InstanceLogLimiter"/> (ADR-011 T-12). English templates, Information level.
/// </summary>
public sealed class UsageHistoryLog(ILogger logger, InstanceLogLimiter limiter)
{
    /// <summary>A new app record (AC-7). Not limited: it happens once per account and app.</summary>
    public void NewApp(string userName, string displayName, string processName, string programPath)
    {
        logger.LogInformation(
            "New app for account {UserName}: {DisplayName} ({ProcessName}, {ProgramPath}).", userName, displayName, processName, programPath);
    }

    /// <summary>An instance started (AC-10).</summary>
    public void Started(string accountSid, string userName, string displayName, string processName, string programPath, long instanceId)
    {
        if (limiter.TryAcquire(accountSid, programPath, userName, displayName))
        {
            logger.LogInformation(
                "App started: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), instance {InstanceId}.",
                userName, displayName, processName, programPath, instanceId);
        }
    }

    /// <summary>An instance ended (AC-10), with its duration from start to end.</summary>
    public void Ended(
        string accountSid, string userName, string displayName, string processName, string programPath, long instanceId,
        TimeSpan duration, string reason)
    {
        if (limiter.TryAcquire(accountSid, programPath, userName, displayName))
        {
            logger.LogInformation(
                "App ended: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), instance {InstanceId}, duration {Duration} ({Reason}).",
                userName, displayName, processName, programPath, instanceId, FormatDuration(duration), reason);
        }
    }

    /// <summary>Writes one summary line per app whose limit was exceeded in an hour that is over.</summary>
    public void Flush()
    {
        foreach (var summary in limiter.TakeSummaries())
        {
            logger.LogInformation(
                "{Count} further start/end entries of account {UserName}, {DisplayName} ({ProgramPath}) were not logged in the last hour.",
                summary.Count, summary.UserName, summary.DisplayName, summary.ProgramPath);
        }
    }

    /// <summary><c>hh:mm:ss</c>; negative durations (clock changed) are shown as 00:00:00.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var value = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        return string.Create(CultureInfo.InvariantCulture, $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}");
    }
}
