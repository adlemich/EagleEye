using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>Runs the close sequences of blocked starts outside the accounting loop (ADR-013 §4, US-005 Decision 4).</summary>
public interface IBlockedStartRunner
{
    /// <summary>Starts the close sequence of a blocked start; returns at once.</summary>
    IBlockedStartHandle Start(BlockedStart start);

    /// <summary>Service stop: every running sequence terminates its kill set at once and records it (≤ 2 s, D-14).</summary>
    Task StopAsync();
}

/// <summary>A running close sequence, as the gate sees it.</summary>
public interface IBlockedStartHandle
{
    /// <summary>Whether the sequence has ended (the app is gone or could not be ended).</summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Adds window processes of a second start of the same app while it is being closed (story OQ-17): they get their
    /// own close command and are covered by the sequence's force step; no own record, no second message.
    /// </summary>
    void AddProcesses(IReadOnlyList<ObservedProcess> processes);
}
