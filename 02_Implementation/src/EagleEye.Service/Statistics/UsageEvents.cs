using EagleEye.Service.Monitoring;

namespace EagleEye.Service.Statistics;

/// <summary>An app of one account as seen in one session: all its processes with an app window (ADR-012 §1).</summary>
/// <param name="ProgramPath">Full program path, the app's identity per account (case-insensitive).</param>
/// <param name="ProcessName">The process name, e.g. <c>Code.exe</c>.</param>
/// <param name="DisplayName">The resolved display name (AC-8).</param>
/// <param name="ProcessIds">The processes of the app that have an app window.</param>
public sealed record ObservedApp(string ProgramPath, string ProcessName, string DisplayName, IReadOnlyList<int> ProcessIds);

/// <summary>Input of the accounting loop (one queue, one reader).</summary>
public abstract record UsageEvent;

/// <summary>The latest report of the session agent of a session, validated and named.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="AccountSid">The session's user.</param>
/// <param name="Apps">The apps open in the session.</param>
public sealed record AppsObserved(int SessionId, string AccountSid, IReadOnlyList<ObservedApp> Apps) : UsageEvent;

/// <summary>A session change notification of the Service Control Manager.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="Kind">What happened.</param>
public sealed record SessionChanged(int SessionId, SessionChangeKind Kind) : UsageEvent;

/// <summary>The PC goes to sleep or hibernation.</summary>
public sealed record PowerSuspended : UsageEvent;

/// <summary>The PC resumed from sleep or hibernation.</summary>
public sealed record PowerResumed : UsageEvent;

/// <summary>The 5 s accounting tick (ADR-012 §4).</summary>
public sealed record UsageTick : UsageEvent;

/// <summary>The reasons an app instance ends (US-004 AC-10, ADR-012 §1).</summary>
public static class EndReasons
{
    /// <summary>The last app window of the program was closed.</summary>
    public const string Closed = "closed";

    /// <summary>The account was unticked or became an admin.</summary>
    public const string MonitoringStopped = "monitoring stopped";

    /// <summary>The session was logged off.</summary>
    public const string SessionEnded = "session ended";

    /// <summary>The service is stopping.</summary>
    public const string ServiceStopping = "service stopping";

    /// <summary>The instance was left open by a crash or power loss; ended at the next start.</summary>
    public const string ServiceStoppedUnexpectedly = "service stopped unexpectedly";
}
