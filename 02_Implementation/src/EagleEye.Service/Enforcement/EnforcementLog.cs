using System.Globalization;
using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Log entries of blocked starts and enforced closes (US-005 AC-28, US-006 AC-37, plan "Logging"). Two lines per close
/// (detection, result). Blocked starts are limited like the instance entries of US-004 (Q-4): at most
/// <see cref="InstanceLogLimiter.MaxEntriesPerHour"/> per account and app and hour, for all reasons together, then one
/// summary line per hour (<see cref="Flush"/>); closes of running apps are not limited. The history keeps every record.
/// Thread-safe: close sequences run in parallel.
/// </summary>
public sealed class EnforcementLog(ILogger<EnforcementLog> logger, InstanceLogLimiter limiter)
{
    private readonly Lock _lock = new();

    /// <summary>Logs the detection (blocked starts: unless the limit is reached); returns whether it was logged.</summary>
    public bool Detected(CloseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind == CloseKinds.RunningApp)
        {
            logger.LogInformation(
                "Closing app: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), {Reason}{AfterGrace}.",
                request.UserName, request.DisplayName, request.ProcessName, request.ProgramPath, ReasonText(request),
                request.AfterGrace ? ", after grace period" : string.Empty);
            return true;
        }

        bool allowed;
        lock (_lock)
        {
            allowed = limiter.TryAcquire(request.AccountSid, request.ProgramPath, request.UserName, request.DisplayName);
        }

        if (!allowed)
        {
            return false;
        }

        if (request.Entry is { } entry)
        {
            logger.LogInformation(
                "Blocked start: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), process {ProcessId}, break time {Range} ({Days}); closing the app.",
                request.UserName, request.DisplayName, request.ProcessName, request.ProgramPath, request.Targets[0].Pid,
                BreakTimeChangeLog.Range(entry), BreakTimeChangeLog.Days(entry.Days));
        }
        else
        {
            logger.LogInformation(
                "Blocked start: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), process {ProcessId}, {Reason} ({Rule}); closing the app.",
                request.UserName, request.DisplayName, request.ProcessName, request.ProgramPath, request.Targets[0].Pid, request.Reason, RuleText(request.Rule));
        }

        return true;
    }

    /// <summary>Logs the result of a close whose detection was logged.</summary>
    public void Ended(CloseRequest request, string outcome, string messageState, bool detectionLogged)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!detectionLogged)
        {
            return;
        }

        logger.LogInformation(
            "{What}: account {UserName}, {DisplayName} ({ProcessName}, {ProgramPath}), {Reason}: {Outcome}; message {MessageState}.",
            request.Kind == CloseKinds.RunningApp ? "App closed" : "Blocked start ended",
            request.UserName, request.DisplayName, request.ProcessName, request.ProgramPath, ReasonText(request), outcome, messageState);
    }

    /// <summary>Error: processes of an app were still running after all force rounds.</summary>
    public void NotEnded(CloseRequest request, int remaining)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogError(
            "The blocked app {DisplayName} ({ProgramPath}) of account {UserName} could not be ended: {Remaining} process(es) still running after 3 rounds.",
            request.DisplayName, request.ProgramPath, request.UserName, remaining);
    }

    /// <summary>Warning: a process could not be terminated.</summary>
    public void TerminateFailed(CloseRequest request, int pid, int win32Error)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogWarning(
            "Process {ProcessId} of the blocked app {DisplayName} ({ProgramPath}) could not be terminated (Win32 error {Error}).",
            pid, request.DisplayName, request.ProgramPath, win32Error);
    }

    /// <summary>Warning: the history record could not be stored; the sequence goes on.</summary>
    public void HistoryFailed(CloseRequest request, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogWarning(exception, "Storing the history record of a blocked start of {DisplayName} ({ProgramPath}) failed.", request.DisplayName, request.ProgramPath);
    }

    /// <summary>Warning: the app record of a blocked start could not be created; the app is still closed.</summary>
    public void AppRecordFailed(CloseRequest request, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.LogWarning(exception, "The app record for the blocked start of {DisplayName} ({ProgramPath}) could not be created.", request.DisplayName, request.ProgramPath);
    }

    /// <summary>"Warning: account kid1, Editor (notepad.exe), early warning, 2 min left (budget); message shown." (AC-37).</summary>
    public void AppWarning(string userName, string displayName, string processName, WarningLevel level, int minutes, AppEndKind kind, string messageState)
    {
        logger.LogInformation(
            "Warning: account {UserName}, {DisplayName} ({ProcessName}), {Level} warning, {Minutes} min left ({End}); message {MessageState}.",
            userName, displayName, processName, LevelText(level), minutes, EndText(kind), messageState);
    }

    /// <summary>"Warning: account kid1, break time 20:00–23:59 (Sa) begins at 20:00, final warning, 1 min left; message shown." (AC-37).</summary>
    public void BreakWarning(string userName, BreakTimeEntryDto entry, DateTime beginsLocal, WarningLevel level, int minutes, string messageState)
    {
        ArgumentNullException.ThrowIfNull(entry);
        logger.LogInformation(
            "Warning: account {UserName}, break time {Range} ({Days}) begins at {Begins}, {Level} warning, {Minutes} min left; message {MessageState}.",
            userName, BreakTimeChangeLog.Range(entry), BreakTimeChangeLog.Days(entry.Days), beginsLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            LevelText(level), minutes, messageState);
    }

    /// <summary>"Grace period: account kid1, Editor (notepad.exe): the rules changed (budget); closes in 1 min at 18:31:05; message shown." (AC-37).</summary>
    public void AppGrace(string userName, string displayName, string processName, AppEndKind kind, int minutes, DateTime closesLocal, string messageState)
    {
        logger.LogInformation(
            "Grace period: account {UserName}, {DisplayName} ({ProcessName}): the rules changed ({End}); closes in {Minutes} min at {Closes}; message {MessageState}.",
            userName, displayName, processName, EndText(kind), minutes, closesLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture), messageState);
    }

    /// <summary>"Grace period: account kid1, break time 20:00–23:59 (Sa) begins at 20:00; 2 app(s) close in 1 min at 19:59:35; message shown." (AC-37).</summary>
    public void BreakGrace(string userName, BreakTimeEntryDto entry, DateTime beginsLocal, int count, int minutes, DateTime closesLocal, string messageState)
    {
        ArgumentNullException.ThrowIfNull(entry);
        logger.LogInformation(
            "Grace period: account {UserName}, break time {Range} ({Days}) begins at {Begins}; {Count} app(s) close in {Minutes} min at {Closes}; message {MessageState}.",
            userName, BreakTimeChangeLog.Range(entry), BreakTimeChangeLog.Days(entry.Days), beginsLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            count, minutes, closesLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture), messageState);
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

    /// <summary>"break time 20:00–23:59 (Sa)", "budget used up" or "not allowed today".</summary>
    internal static string ReasonText(CloseRequest request) =>
        request.Entry is { } entry ? $"break time {BreakTimeChangeLog.Range(entry)} ({BreakTimeChangeLog.Days(entry.Days)})" : request.Reason;

    /// <summary>"rule 0:45, Mo Tu, used 0:45:03".</summary>
    internal static string RuleText(AppRuleAtClose? rule) =>
        rule is null
            ? "rule unknown"
            : $"rule {AppRuleRules.Format(rule.BudgetMinutes)}, {AppRuleChangeLog.Days(rule.Days)}, used {Duration(rule.UsedSeconds)}";

    /// <summary>"early" or "final".</summary>
    internal static string LevelText(WarningLevel level) => level == WarningLevel.Early ? "early" : "final";

    /// <summary>"budget", "not allowed today", "midnight" or "break time".</summary>
    internal static string EndText(AppEndKind kind) => kind switch
    {
        AppEndKind.Budget => "budget",
        AppEndKind.NotAllowedToday => "not allowed today",
        AppEndKind.Midnight => "midnight",
        _ => "break time",
    };

    /// <summary>"H:MM:SS".</summary>
    internal static string Duration(long seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}");
    }
}
