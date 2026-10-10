using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>A blocked start detected by the <see cref="BreakTimeGate"/> (ADR-013 §3).</summary>
/// <param name="SessionId">The session of the start.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="UserName">The account's user name.</param>
/// <param name="ProgramPath">The app's program path.</param>
/// <param name="ProcessName">The process name.</param>
/// <param name="DisplayName">The app's display name.</param>
/// <param name="Targets">The reported window processes of the app (PID + creation time).</param>
/// <param name="Trigger"><see cref="BlockedStartTexts.AppStartTrigger"/> or <see cref="BlockedStartTexts.AgentStartTrigger"/>.</param>
/// <param name="Entry">The entry in effect at the start (first by entry order).</param>
/// <param name="DisplayText">The account's display text at detection (AC-31).</param>
/// <param name="StartedUtc">Creation time of the app's first process.</param>
/// <param name="StartedLocal">The same, local time of the service PC.</param>
/// <param name="DetectedUtc">When the gate detected the blocked start.</param>
public sealed record BlockedStart(
    int SessionId,
    string AccountSid,
    string UserName,
    string ProgramPath,
    string ProcessName,
    string DisplayName,
    IReadOnlyList<ObservedProcess> Targets,
    string Trigger,
    BreakTimeEntryDto Entry,
    string DisplayText,
    DateTimeOffset StartedUtc,
    DateTime StartedLocal,
    DateTimeOffset DetectedUtc);

/// <summary>Fixed texts of blocked starts in the history and the log (English, arc42 §8.13).</summary>
public static class BlockedStartTexts
{
    /// <summary>Trigger: the app was started while the agent was running.</summary>
    public const string AppStartTrigger = "app start";

    /// <summary>Trigger: the app was found in the first report of an agent run (sign-in, agent or service restart).</summary>
    public const string AgentStartTrigger = "found at agent start";

    /// <summary>Outcome: every process of the kill set ended within 20 s.</summary>
    public const string ClosedGracefully = "closed gracefully";

    /// <summary>Outcome: something was left at 20 s and was terminated.</summary>
    public const string TerminatedByForce = "terminated by force";

    /// <summary>Outcome: the service stopped during the sequence and terminated the rest at once.</summary>
    public const string TerminatedServiceStopping = "terminated by force (service stopping)";

    /// <summary>Outcome of a record left open by a crash, set at the next start.</summary>
    public const string UnknownServiceStopped = "unknown (service stopped)";
}
