using EagleEye.Service.Data;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// A request to close an app (ADR-013 §3, §4; ADR-015 §7; US-006 Decision 8): a blocked start detected by the
/// <see cref="AppStartGate"/>, or a running app closed by the time-limit engine.
/// </summary>
/// <param name="SessionId">The session of the app.</param>
/// <param name="AccountSid">The account.</param>
/// <param name="UserName">The account's user name.</param>
/// <param name="ProgramPath">The app's program path.</param>
/// <param name="ProcessName">The process name.</param>
/// <param name="DisplayName">The app's display name.</param>
/// <param name="Targets">The reported window processes of the app (PID + creation time).</param>
/// <param name="Trigger">One of the triggers of <see cref="BlockedStartTexts"/>.</param>
/// <param name="Entry">The break entry (reason "break time"), else <c>null</c>.</param>
/// <param name="StartedUtc">Creation time of the app's first process.</param>
/// <param name="StartedLocal">The same, local time of the service PC.</param>
/// <param name="DetectedUtc">Blocked start: when the gate detected it; running app: when the close began.</param>
public sealed record CloseRequest(
    int SessionId,
    string AccountSid,
    string UserName,
    string ProgramPath,
    string ProcessName,
    string DisplayName,
    IReadOnlyList<ObservedProcess> Targets,
    string Trigger,
    BreakTimeEntryDto? Entry,
    DateTimeOffset StartedUtc,
    DateTime StartedLocal,
    DateTimeOffset DetectedUtc)
{
    /// <summary>One of <see cref="CloseKinds"/>.</summary>
    public string Kind { get; init; } = CloseKinds.BlockedStart;

    /// <summary>One of <see cref="CloseReasons"/>.</summary>
    public string Reason { get; init; } = CloseReasons.BreakTime;

    /// <summary>Whether the close follows a grace period (US-006 AC-32, AC-33).</summary>
    public bool AfterGrace { get; init; }

    /// <summary>The app rule at that moment (budget reasons), else <c>null</c>.</summary>
    public AppRuleAtClose? Rule { get; init; }

    /// <summary>The kid's message of this close; <c>null</c> when the message is sent once for a batch (<see cref="SharedMessageState"/>).</summary>
    public KidMessageDto? Message { get; init; }

    /// <summary>The message state of a batch (one break-time message per session for all its apps, AC-29), or <c>null</c>.</summary>
    public Task<string>? SharedMessageState { get; init; }

    /// <summary>Whether the app's processes are removed from the report while it is closed (blocked starts only, TI-8).</summary>
    public bool RemovesFromReport => Kind == CloseKinds.BlockedStart;
}

/// <summary>Fixed texts of blocked starts and enforced closes in the history and the log (English, arc42 §8.13).</summary>
public static class BlockedStartTexts
{
    /// <summary>Trigger: the app was started while the agent was running.</summary>
    public const string AppStartTrigger = "app start";

    /// <summary>Trigger: the app was found in the first report of an agent run (sign-in, agent or service restart).</summary>
    public const string AgentStartTrigger = "found at agent start";

    /// <summary>Trigger of a running-app close: a break time began.</summary>
    public const string BreakBeganTrigger = "break began";

    /// <summary>Trigger of a running-app close: the budget ran out.</summary>
    public const string BudgetRanOutTrigger = "budget ran out";

    /// <summary>Trigger of a running-app close: midnight, the next day does not allow the app.</summary>
    public const string MidnightTrigger = "midnight";

    /// <summary>Trigger of a running-app close: a grace period after a parent change ended.</summary>
    public const string GraceEndedTrigger = "grace period ended";

    /// <summary>Trigger of a running-app close: the rules changed and no time is left (an app first seen without time, or not allowed today).</summary>
    public const string RulesChangedTrigger = "rules changed";

    /// <summary>Outcome: every process of the kill set ended within 20 s.</summary>
    public const string ClosedGracefully = "closed gracefully";

    /// <summary>Outcome: something was left at 20 s and was terminated.</summary>
    public const string TerminatedByForce = "terminated by force";

    /// <summary>Outcome: the service stopped during the sequence and terminated the rest at once.</summary>
    public const string TerminatedServiceStopping = "terminated by force (service stopping)";

    /// <summary>Outcome of a record left open by a crash, set at the next start.</summary>
    public const string UnknownServiceStopped = "unknown (service stopped)";
}
