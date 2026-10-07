namespace EagleEye.Service.Monitoring;

/// <summary>
/// The sessions of the PC as the accounting loop sees them (ADR-012 §2): full snapshots from the WTS API at every
/// tick, and SCM session notifications in between, applied at once. Used only by the accounting loop.
/// </summary>
public sealed class SessionStateTracker
{
    private readonly Dictionary<int, SessionInfo> _sessions = [];

    /// <summary>All known sessions.</summary>
    public IReadOnlyCollection<SessionInfo> Sessions => _sessions.Values;

    /// <summary>Replaces the state with a fresh WTS snapshot (reconciliation).</summary>
    public void ApplySnapshot(IReadOnlyList<SessionInfo> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions.Clear();
        foreach (var session in sessions)
        {
            _sessions[session.SessionId] = session;
        }
    }

    /// <summary>Applies a notification. Unknown sessions are ignored (the next snapshot adds them).</summary>
    public void ApplyChange(int sessionId, SessionChangeKind kind)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        if (kind == SessionChangeKind.Logoff)
        {
            _sessions.Remove(sessionId);
            return;
        }

        _sessions[sessionId] = kind switch
        {
            SessionChangeKind.Lock => session with { IsLocked = true },
            SessionChangeKind.Unlock => session with { IsLocked = false },
            SessionChangeKind.ConsoleDisconnect or SessionChangeKind.RemoteDisconnect => session with { State = SessionConnectState.Disconnected },
            _ => session with { State = SessionConnectState.Active },
        };
    }

    /// <summary>Whether the session exists and is in use (active, unlocked).</summary>
    public bool IsInUse(int sessionId) => _sessions.TryGetValue(sessionId, out var session) && session.IsInUse;

    /// <summary>The sessions of an account.</summary>
    public IReadOnlyList<SessionInfo> SessionsOf(string accountSid)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        return [.. _sessions.Values.Where(s => StringComparer.OrdinalIgnoreCase.Equals(s.UserSid, accountSid))];
    }

    /// <summary>
    /// Whether the account's apps count as active: one of its sessions is in use and observed by a working agent
    /// (<paramref name="isObserved"/>; a paused agent means no credit, ADR-011 §7 item 18).
    /// </summary>
    public bool AccountInUse(string accountSid, Func<int, bool> isObserved)
    {
        ArgumentNullException.ThrowIfNull(isObserved);
        return SessionsOf(accountSid).Any(s => s.IsInUse && isObserved(s.SessionId));
    }
}
