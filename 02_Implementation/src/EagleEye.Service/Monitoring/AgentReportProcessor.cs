using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Turns an agent's report (untrusted PIDs, ADR-011 §3 and §7 item 11) into the apps of the account:
/// <list type="bullet">
/// <item>Each PID must run in the agent's session and belong to the session user (US-004 AC-1, AC-26); a PID is
/// identified with its creation time, so a reused PID is inspected again.</item>
/// <item>EagleEye's own programs are dropped by their path in the installation folder (AC-5, T-10).</item>
/// <item>Store apps count only behind the real ApplicationFrameHost; otherwise the frame's own process counts.
/// Windows of the real <c>explorer.exe</c> count only as File Explorer windows; a program merely <i>named</i>
/// <c>explorer.exe</c> counts like any other (AC-4, T-10).</item>
/// <item>Processes with the same program path are one app (AC-6).</item>
/// </list>
/// Full inspection runs once per PID and creation time; the result goes to the accounting queue (latest-only).
/// </summary>
public sealed class AgentReportProcessor(
    IProcessInspector inspector,
    AppNameResolver names,
    ProgramPathPolicy policy,
    UsageEventQueue queue,
    TimeProvider timeProvider,
    ILogger<AgentReportProcessor> logger) : IAgentReportSink
{
    /// <summary>Minimum time between two "truncated" warnings of one session.</summary>
    public static readonly TimeSpan TruncationWarningInterval = TimeSpan.FromHours(1);

    private readonly Dictionary<int, Dictionary<(int Pid, long Created), ProcessFacts?>> _cache = [];
    private readonly Dictionary<int, long> _truncationWarned = [];
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public void Process(int sessionId, string accountSid, long agentRun, AgentReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountSid);
        ArgumentNullException.ThrowIfNull(report);
        try
        {
            WarnIfTruncated(sessionId, report);
            var apps = Collect(sessionId, accountSid, report);
            queue.Enqueue(new AppsObserved(sessionId, accountSid, apps, agentRun));
        }
        catch (Exception ex)
        {
            // Report boundary: a failing report is skipped; the next one (≤ 5 s) is processed normally.
            logger.LogWarning(ex, "Processing a report of the session agent in session {SessionId} failed.", sessionId);
        }
    }

    private List<ObservedApp> Collect(int sessionId, string accountSid, AgentReport report)
    {
        Dictionary<(int, long), ProcessFacts?> previous;
        lock (_lock)
        {
            previous = _cache.GetValueOrDefault(sessionId) ?? [];
        }

        var current = new Dictionary<(int, long), ProcessFacts?>();
        var apps = new Dictionary<string, (ProcessFacts Facts, SortedDictionary<int, long> Processes)>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in report.Apps)
        {
            if (Effective(app, sessionId, accountSid, previous, current) is not { } facts)
            {
                continue;
            }

            if (!apps.TryGetValue(facts.ImagePath, out var entry))
            {
                entry = (facts, []);
                apps[facts.ImagePath] = entry;
            }

            entry.Processes[facts.ProcessId] = facts.CreationTime;
        }

        lock (_lock)
        {
            _cache[sessionId] = current;
        }

        return [.. apps.Values.Select(a => new ObservedApp(
            a.Facts.ImagePath, Path.GetFileName(a.Facts.ImagePath), names.Resolve(a.Facts),
            [.. a.Processes.Select(p => new ObservedProcess(p.Key, p.Value))]))];
    }

    /// <summary>The process that counts for the reported app, or null if nothing counts.</summary>
    private ProcessFacts? Effective(
        AgentApp app, int sessionId, string accountSid,
        Dictionary<(int, long), ProcessFacts?> previous, Dictionary<(int, long), ProcessFacts?> current)
    {
        var facts = Belonging(app.Pid, sessionId, accountSid, previous, current);
        switch (app.Kind)
        {
            case AgentAppKind.StoreApp:
                var host = Belonging(app.HostPid!.Value, sessionId, accountSid, previous, current); // The protocol requires a host.
                return host is not null && policy.IsSystemFrameHost(host.ImagePath) ? facts : host;
            case AgentAppKind.ExplorerWindow:
                return facts is not null && policy.IsSystemExplorer(facts.ImagePath) ? null : facts;
            default:
                return facts;
        }
    }

    /// <summary>The process's facts if it runs in the session, belongs to the user and is not EagleEye's own.</summary>
    private ProcessFacts? Belonging(
        int pid, int sessionId, string accountSid,
        Dictionary<(int, long), ProcessFacts?> previous, Dictionary<(int, long), ProcessFacts?> current)
    {
        if (inspector.GetCreationTime(pid) is not { } created)
        {
            return null;
        }

        var key = (pid, created);
        if (!current.TryGetValue(key, out var facts))
        {
            facts = previous.TryGetValue(key, out var cached) ? cached : inspector.Inspect(pid);
            current[key] = facts;
        }

        return facts is not null
            && facts.CreationTime == created
            && facts.SessionId == sessionId
            && StringComparer.OrdinalIgnoreCase.Equals(facts.OwnerSid, accountSid)
            && !policy.IsEagleEyeProgram(facts.ImagePath)
            ? facts
            : null;
    }

    private void WarnIfTruncated(int sessionId, AgentReport report)
    {
        if (!report.Truncated)
        {
            return;
        }

        var now = timeProvider.GetTimestamp();
        lock (_lock)
        {
            if (_truncationWarned.TryGetValue(sessionId, out var last) && timeProvider.GetElapsedTime(last, now) < TruncationWarningInterval)
            {
                return;
            }

            _truncationWarned[sessionId] = now;
        }

        logger.LogWarning(
            "The session agent in session {SessionId} found more than {MaxWindows} windows; its report is incomplete.",
            sessionId, WindowEnumerator.MaxWindows);
    }
}
