using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>The apps of one session as last reported and screened (passed to the tracker).</summary>
/// <param name="SessionId">The session.</param>
/// <param name="AccountSid">The session's user.</param>
/// <param name="Apps">The apps with their window processes.</param>
public sealed record SessionApps(int SessionId, string AccountSid, IReadOnlyList<ObservedApp> Apps);

/// <summary>
/// The last screened apps per session (US-006 Decision 5): the time-limit engine evaluates them, and a running-app close
/// targets their window processes. Updated by the accounting loop after every screened report; used by the loop only.
/// </summary>
public sealed class SessionAppsView
{
    private readonly Dictionary<int, SessionApps> _sessions = [];

    /// <summary>All sessions with a report.</summary>
    public IReadOnlyCollection<SessionApps> Sessions => _sessions.Values;

    /// <summary>Replaces the apps of the session.</summary>
    public void Update(int sessionId, string accountSid, IReadOnlyList<ObservedApp> apps)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(apps);
        _sessions[sessionId] = new SessionApps(sessionId, accountSid, apps);
    }

    /// <summary>The apps of the session, or <c>null</c>.</summary>
    public SessionApps? Of(int sessionId) => _sessions.GetValueOrDefault(sessionId);

    /// <summary>The session ended.</summary>
    public void Remove(int sessionId) => _sessions.Remove(sessionId);
}
