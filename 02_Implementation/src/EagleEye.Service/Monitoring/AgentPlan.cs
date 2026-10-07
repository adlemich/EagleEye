namespace EagleEye.Service.Monitoring;

/// <summary>The supervisor's view of one agent slot (one per session).</summary>
/// <param name="AccountSid">The session user the agent was started for.</param>
/// <param name="IsRunning">An agent process is running.</param>
/// <param name="RestartDue">No agent is running and its back-off has passed.</param>
public sealed record AgentSlotState(string AccountSid, bool IsRunning, bool RestartDue);

/// <summary>Sessions that need an agent started, and sessions whose agent must stop.</summary>
public sealed record AgentPlanResult(IReadOnlyList<SessionInfo> Start, IReadOnlyList<int> Stop);

/// <summary>
/// Pure decisions of the supervisor (ADR-011 §2): one agent per logged-on session (active or switched away) of an
/// account under parental control; never for session 0, sessions without a user, uncontrolled or admin accounts
/// (US-004 AC-1). Restart back-off after unexpected exits: 1, 5, then 30 s; the count starts again after an agent
/// ran for a stable minute.
/// </summary>
public static class AgentPlan
{
    /// <summary>An agent that ran at least this long before it exited starts a new failure streak.</summary>
    public static readonly TimeSpan StableRunTime = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan[] RestartDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    /// <summary>Whether the session gets an agent.</summary>
    public static bool IsEligible(SessionInfo session, IReadOnlySet<string> controlledSids)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(controlledSids);
        return session.SessionId != 0 && session.IsLoggedOn && controlledSids.Contains(session.UserSid!); // IsLoggedOn implies a user.
    }

    /// <summary>Computes which agents to start and which to stop.</summary>
    public static AgentPlanResult Compute(
        IEnumerable<SessionInfo> sessions, IReadOnlySet<string> controlledSids, IReadOnlyDictionary<int, AgentSlotState> slots)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(slots);
        var eligible = sessions.Where(s => IsEligible(s, controlledSids)).ToDictionary(s => s.SessionId);
        var stop = slots
            .Where(s => s.Value.IsRunning
                && (!eligible.TryGetValue(s.Key, out var session) || !StringComparer.OrdinalIgnoreCase.Equals(session.UserSid, s.Value.AccountSid)))
            .Select(s => s.Key)
            .Order()
            .ToList();
        var start = eligible.Values
            .Where(s => !slots.TryGetValue(s.SessionId, out var slot) || (!slot.IsRunning && slot.RestartDue))
            .OrderBy(s => s.SessionId)
            .ToList();
        return new AgentPlanResult(start, stop);
    }

    /// <summary>The wait before the next start after <paramref name="consecutiveFailures"/> failures in a row (≥ 1).</summary>
    public static TimeSpan RestartDelay(int consecutiveFailures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(consecutiveFailures, 1);
        return RestartDelays[Math.Min(consecutiveFailures, RestartDelays.Length) - 1];
    }

    /// <summary>The failure count after an agent failed that had run for <paramref name="runTime"/>.</summary>
    public static int NextFailureCount(int previousFailures, TimeSpan runTime)
    {
        return runTime >= StableRunTime ? 1 : previousFailures + 1;
    }
}
