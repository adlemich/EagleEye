using EagleEye.Service.SessionAgent;

namespace EagleEye.Service.Monitoring;

/// <summary>Receives validated agent reports (implemented by <see cref="AgentReportProcessor"/>).</summary>
public interface IAgentReportSink
{
    /// <summary>Processes the latest report of the agent of a session; <paramref name="agentRun"/> changes on every agent start.</summary>
    void Process(int sessionId, string accountSid, long agentRun, AgentReport report);
}

/// <summary>Keeps one session agent per watched session (ADR-011 §2, §7 item 18).</summary>
public interface ISessionAgentSupervisor
{
    /// <summary>Starts, stops and restarts agents for the sessions and controlled accounts (SID → user name).</summary>
    Task ReconcileAsync(IReadOnlyCollection<SessionInfo> sessions, IReadOnlyDictionary<string, string> controlledAccounts);

    /// <summary>Whether the session is observed by a working agent (reported within the heartbeat limit).</summary>
    bool IsObserving(int sessionId);

    /// <summary>Stops all agents (service stopping).</summary>
    Task StopAllAsync();

    /// <summary>
    /// Asks the agent of the session to post <c>WM_CLOSE</c> to the app windows of the targets (ADR-013 §4) and waits for
    /// its answer at most <see cref="SessionAgentSupervisor.CloseAnswerTimeout"/>. <c>null</c>: no working agent, the
    /// command could not be written, or no answer in time.
    /// </summary>
    Task<CloseAnswer?> RequestCloseAsync(int sessionId, IReadOnlyList<CloseTarget> targets, CancellationToken ct = default);
}

/// <summary>
/// Supervises the session agents. Reports are read in the background and handed to <see cref="IAgentReportSink"/>;
/// every reconcile (each 5 s tick) handles exits, protocol errors and missing heartbeats (15 s): recording for the
/// session is paused (<see cref="IsObserving"/> is false, so nothing is credited and instances stay open), the
/// agent is restarted with back-off (1, 5, 30 s) and a Warning is logged; after 3 failed restarts in a row an
/// Error is logged once, and retries go on every 30 s (ADR-011 §7 item 18, T-9).
/// </summary>
public sealed class SessionAgentSupervisor(
    IAgentLauncher launcher,
    IAgentReportSink sink,
    TimeProvider timeProvider,
    ILogger<SessionAgentSupervisor> logger) : ISessionAgentSupervisor
{
    /// <summary>An agent that did not report for this long is restarted.</summary>
    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Failures in a row (the first exit plus 3 failed restarts) after which the Error is logged.</summary>
    public const int FailuresForError = 4;

    /// <summary>How long the service waits for the agent's answer to a close command (US-005 Decision 5).</summary>
    public static readonly TimeSpan CloseAnswerTimeout = TimeSpan.FromSeconds(2);

    private readonly Dictionary<int, Slot> _slots = [];
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _stopping = new();
    private long _lastRun;
    private long _lastCommandId;

    /// <inheritdoc />
    public async Task ReconcileAsync(IReadOnlyCollection<SessionInfo> sessions, IReadOnlyDictionary<string, string> controlledAccounts)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(controlledAccounts);
        foreach (var slot in Snapshot())
        {
            await CheckHealthAsync(slot).ConfigureAwait(false);
        }

        var controlled = controlledAccounts.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plan = AgentPlan.Compute(sessions, controlled, SlotStates());
        foreach (var sessionId in plan.Stop)
        {
            await StopSlotAsync(SlotOf(sessionId)).ConfigureAwait(false);
        }

        RemoveIdleSlots(sessions, controlled);
        foreach (var session in plan.Start)
        {
            Launch(session, controlledAccounts.GetValueOrDefault(session.UserSid!, session.UserSid!)); // Eligible sessions have a user.
        }
    }

    /// <inheritdoc />
    public bool IsObserving(int sessionId)
    {
        lock (_lock)
        {
            return _slots.TryGetValue(sessionId, out var slot)
                && slot.Agent is not null
                && slot.Reported
                && slot.Fault is null
                && timeProvider.GetElapsedTime(slot.LastReport) <= HeartbeatTimeout;
        }
    }

    /// <inheritdoc />
    public async Task StopAllAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        foreach (var slot in Snapshot())
        {
            await StopSlotAsync(slot).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<CloseAnswer?> RequestCloseAsync(int sessionId, IReadOnlyList<CloseTarget> targets, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        IAgentProcess? agent;
        Slot? slot;
        long id;
        var answer = new TaskCompletionSource<CloseAnswer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (!_slots.TryGetValue(sessionId, out slot) || slot.Agent is null || targets.Count == 0)
            {
                return null;
            }

            agent = slot.Agent;
            id = ++_lastCommandId;
            slot.Pending[id] = answer;
        }

        try
        {
            await agent.WriteLineAsync(AgentProtocol.SerializeCloseCommand(new CloseCommand(id, targets)), ct).ConfigureAwait(false);
            return await answer.Task.WaitAsync(CloseAnswerTimeout, timeProvider, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Channel boundary (broken pipe, timeout): the force step of the close sequence still ends the app.
            logger.LogDebug(ex, "The close command {CommandId} to the session agent in session {SessionId} was not answered.", id, sessionId);
            return null;
        }
        finally
        {
            lock (_lock)
            {
                slot.Pending.Remove(id);
            }
        }
    }

    private void CompleteAnswer(Slot slot, IAgentProcess agent, CloseAnswer answer)
    {
        TaskCompletionSource<CloseAnswer?>? pending = null;
        lock (_lock)
        {
            if (ReferenceEquals(slot.Agent, agent))
            {
                slot.LastReport = timeProvider.GetTimestamp();
                slot.Pending.TryGetValue(answer.Id, out pending);
            }
        }

        pending?.TrySetResult(answer);
    }

    private async Task CheckHealthAsync(Slot slot)
    {
        IAgentProcess? agent;
        string? problem;
        lock (_lock)
        {
            agent = slot.Agent;
            problem = agent is null ? null
                : agent.Exited.IsCompleted ? $"exited with code {agent.Exited.Result}"
                : slot.Fault ?? (timeProvider.GetElapsedTime(slot.LastReport) > HeartbeatTimeout ? "no report for 15 s" : null);
        }

        if (agent is null || problem is null)
        {
            return;
        }

        await StopAgentAsync(agent).ConfigureAwait(false);
        Fail(slot, problem);
    }

    private void Launch(SessionInfo session, string userName)
    {
        Slot slot;
        bool isNew;
        lock (_lock)
        {
            isNew = !_slots.TryGetValue(session.SessionId, out var existing);
            slot = existing ?? new Slot(session.SessionId, session.UserSid!, userName); // Eligible sessions have a user.
            _slots[session.SessionId] = slot;
        }

        IAgentProcess agent;
        try
        {
            agent = launcher.Launch(session.SessionId);
        }
        catch (Exception ex)
        {
            // Win32 boundary: starting can fail for many reasons; retry with back-off.
            logger.LogWarning(ex, "Starting the session agent in session {SessionId} failed.", session.SessionId);
            Fail(slot, null);
            return;
        }

        lock (_lock)
        {
            slot.Agent = agent;
            slot.StartedAt = timeProvider.GetTimestamp();
            slot.LastReport = slot.StartedAt;
            slot.Reported = false;
            slot.Fault = null;
            slot.Run = ++_lastRun;
        }

        logger.LogInformation("Session agent started in session {SessionId} (process {ProcessId}).", session.SessionId, agent.ProcessId);
        if (isNew)
        {
            logger.LogInformation(
                "Usage recording started for account {UserName} ({AccountSid}) in session {SessionId}.", userName, slot.Sid, session.SessionId);
        }

        _ = ReadReportsAsync(slot, agent, slot.Run);
        _ = ReadErrorsAsync(slot, agent);
    }

    private void Fail(Slot slot, string? problem)
    {
        int failures;
        TimeSpan delay;
        bool logError;
        lock (_lock)
        {
            if (slot.Agent is not null && !slot.Reported)
            {
                launcher.ReportEarlyExit();
            }

            var runTime = slot.Agent is null ? TimeSpan.Zero : timeProvider.GetElapsedTime(slot.StartedAt);
            slot.Agent = null;
            slot.Failures = AgentPlan.NextFailureCount(slot.Failures, runTime);
            failures = slot.Failures;
            delay = AgentPlan.RestartDelay(failures);
            slot.RestartAt = timeProvider.GetTimestamp() + (long)(delay.TotalSeconds * timeProvider.TimestampFrequency);
            logError = failures >= FailuresForError && !slot.ErrorLogged;
            slot.ErrorLogged |= logError;
        }

        if (problem is not null)
        {
            logger.LogWarning(
                "Session agent in session {SessionId} {Problem}; recording is paused, restarting in {Delay}.", slot.SessionId, problem, delay);
        }

        if (logError)
        {
            logger.LogError(
                "Usage recording for account {UserName} ({AccountSid}) is not possible: the session agent keeps failing. Retrying every 30 s.",
                slot.UserName, slot.Sid);
        }
    }

    private async Task ReadReportsAsync(Slot slot, IAgentProcess agent, long run)
    {
        try
        {
            while (await agent.ReadReportLineAsync(_stopping.Token).ConfigureAwait(false) is { } line)
            {
                if (AgentProtocol.IsCloseAnswer(line))
                {
                    if (!AgentProtocol.TryParseCloseAnswer(line, out var answer))
                    {
                        MarkFault(slot, agent, "sent an invalid answer");
                        return;
                    }

                    CompleteAnswer(slot, agent, answer);
                    continue;
                }

                if (!AgentProtocol.TryParse(line, out var report))
                {
                    MarkFault(slot, agent, "sent an invalid report");
                    return;
                }

                if (MarkReported(slot, agent))
                {
                    sink.Process(slot.SessionId, slot.Sid, run, report);
                }
            }
        }
        catch (AgentProtocolException ex)
        {
            MarkFault(slot, agent, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Channel boundary: anything unexpected ends reading; the next reconcile restarts the agent.
            logger.LogWarning(ex, "Reading the reports of the session agent in session {SessionId} failed.", slot.SessionId);
            MarkFault(slot, agent, "could not be read");
        }
        catch (OperationCanceledException)
        {
            // Service stopping.
        }
    }

    private async Task ReadErrorsAsync(Slot slot, IAgentProcess agent)
    {
        try
        {
            while (await agent.ReadErrorLineAsync(_stopping.Token).ConfigureAwait(false) is { } line)
            {
                var level = line.StartsWith(AgentDiagnostics.Prefix, StringComparison.Ordinal) ? LogLevel.Information : LogLevel.Warning;
                logger.Log(level, "Session agent in session {SessionId}: {Message}", slot.SessionId, line);
            }
        }
        catch (Exception ex)
        {
            // Diagnostics only: a broken or over-long stderr line ends reading stderr, nothing else.
            logger.LogWarning(ex, "Reading the error output of the session agent in session {SessionId} failed.", slot.SessionId);
        }
    }

    private bool MarkReported(Slot slot, IAgentProcess agent)
    {
        bool first;
        lock (_lock)
        {
            if (!ReferenceEquals(slot.Agent, agent))
            {
                return false;
            }

            first = !slot.Reported;
            slot.Reported = true;
            slot.LastReport = timeProvider.GetTimestamp();
            if (first)
            {
                slot.Failures = 0;
                slot.ErrorLogged = false;
            }
        }

        if (first)
        {
            launcher.ReportWorking();
        }

        return true;
    }

    private void MarkFault(Slot slot, IAgentProcess agent, string fault)
    {
        lock (_lock)
        {
            if (ReferenceEquals(slot.Agent, agent))
            {
                slot.Fault ??= fault;
            }
        }
    }

    private async Task StopSlotAsync(Slot slot)
    {
        IAgentProcess? agent;
        lock (_lock)
        {
            _slots.Remove(slot.SessionId);
            agent = slot.Agent;
        }

        if (agent is not null)
        {
            await StopAgentAsync(agent).ConfigureAwait(false);
            logger.LogInformation("Session agent in session {SessionId} stopped.", slot.SessionId);
        }
    }

    private static async Task StopAgentAsync(IAgentProcess agent)
    {
        await agent.StopAsync().ConfigureAwait(false);
        await agent.DisposeAsync().ConfigureAwait(false);
    }

    private void RemoveIdleSlots(IReadOnlyCollection<SessionInfo> sessions, HashSet<string> controlled)
    {
        lock (_lock)
        {
            foreach (var slot in _slots.Values.Where(s => s.Agent is null).ToList())
            {
                if (!sessions.Any(s => s.SessionId == slot.SessionId && AgentPlan.IsEligible(s, controlled)))
                {
                    _slots.Remove(slot.SessionId);
                }
            }
        }
    }

    private Slot SlotOf(int sessionId)
    {
        lock (_lock)
        {
            return _slots[sessionId];
        }
    }

    private List<Slot> Snapshot()
    {
        lock (_lock)
        {
            return [.. _slots.Values];
        }
    }

    private Dictionary<int, AgentSlotState> SlotStates()
    {
        var now = timeProvider.GetTimestamp();
        lock (_lock)
        {
            return _slots.ToDictionary(
                s => s.Key,
                s => new AgentSlotState(s.Value.Sid, s.Value.Agent is not null, s.Value.RestartAt <= now));
        }
    }

    private sealed class Slot(int sessionId, string sid, string userName)
    {
        public int SessionId { get; } = sessionId;

        public string Sid { get; } = sid;

        public string UserName { get; } = userName;

        public IAgentProcess? Agent { get; set; }

        public long StartedAt { get; set; }

        public long LastReport { get; set; }

        public bool Reported { get; set; }

        public string? Fault { get; set; }

        public int Failures { get; set; }

        public long RestartAt { get; set; }

        public long Run { get; set; }

        public Dictionary<long, TaskCompletionSource<CloseAnswer?>> Pending { get; } = [];

        public bool ErrorLogged { get; set; }
    }
}
