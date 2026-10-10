using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Log entries of blocked starts (AC-28, plan "Log entries"). Two lines per blocked start (detection, result; D-12).
/// Limited like the instance entries of US-004 (Q-4): at most <see cref="InstanceLogLimiter.MaxEntriesPerHour"/>
/// blocked starts per account and app and hour are logged, then one summary line per hour (<see cref="Flush"/>);
/// the history keeps every record. Thread-safe: close sequences run in parallel.
/// </summary>
public sealed class BlockedStartLog(ILogger<BlockedStartLog> logger, InstanceLogLimiter limiter)
{
    private readonly Lock _lock = new();

    /// <summary>Logs the detection unless the limit is reached; returns whether it was logged.</summary>
    public bool Detected(BlockedStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        bool allowed;
        lock (_lock)
        {
            allowed = limiter.TryAcquire(start.AccountSid, start.ProgramPath, start.UserName, start.DisplayName);
        }

        if (allowed)
        {
            logger.LogInformation(
                "Blocked start: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), process {ProcessId}, break time {Range} ({Days}); closing the app.",
                start.UserName, start.DisplayName, start.ProcessName, start.ProgramPath, start.Targets[0].Pid,
                BreakTimeChangeLog.Range(start.Entry), BreakTimeChangeLog.Days(start.Entry.Days));
        }

        return allowed;
    }

    /// <summary>Logs the result of a blocked start whose detection was logged.</summary>
    public void Ended(BlockedStart start, string outcome, string messageState, bool detectionLogged)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!detectionLogged)
        {
            return;
        }

        logger.LogInformation(
            "Blocked start ended: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), break time {Range} ({Days}): {Outcome}; message {MessageState}.",
            start.UserName, start.DisplayName, start.ProcessName, start.ProgramPath,
            BreakTimeChangeLog.Range(start.Entry), BreakTimeChangeLog.Days(start.Entry.Days), outcome, messageState);
    }

    /// <summary>Error: processes of a blocked app were still running after all force rounds.</summary>
    public void NotEnded(BlockedStart start, int remaining)
    {
        ArgumentNullException.ThrowIfNull(start);
        logger.LogError(
            "The blocked app {DisplayName} ({ProgramPath}) of account {UserName} could not be ended: {Remaining} process(es) still running after 3 rounds.",
            start.DisplayName, start.ProgramPath, start.UserName, remaining);
    }

    /// <summary>Warning: a process could not be terminated.</summary>
    public void TerminateFailed(BlockedStart start, int pid, int win32Error)
    {
        ArgumentNullException.ThrowIfNull(start);
        logger.LogWarning(
            "Process {ProcessId} of the blocked app {DisplayName} ({ProgramPath}) could not be terminated (Win32 error {Error}).",
            pid, start.DisplayName, start.ProgramPath, win32Error);
    }

    /// <summary>Warning: the history record could not be stored; the sequence goes on.</summary>
    public void HistoryFailed(BlockedStart start, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(start);
        logger.LogWarning(exception, "Storing the history record of a blocked start of {DisplayName} ({ProgramPath}) failed.", start.DisplayName, start.ProgramPath);
    }

    /// <summary>Writes one summary line per app whose limit window is over and had suppressed entries.</summary>
    public void Flush()
    {
        IReadOnlyList<SuppressedEntries> summaries;
        lock (_lock)
        {
            summaries = limiter.TakeSummaries();
        }

        foreach (var summary in summaries)
        {
            logger.LogInformation(
                "{Count} further blocked starts of account {UserName}, {DisplayName} ({ProgramPath}) were not logged in the last hour.",
                summary.Count, summary.UserName, summary.DisplayName, summary.ProgramPath);
        }
    }
}
