namespace EagleEye.Service.Monitoring;

/// <summary>Starts session agents (ADR-011 §2).</summary>
public interface IAgentLauncher
{
    /// <summary>Starts the agent in the session.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">The agent could not be started.</exception>
    IAgentProcess Launch(int sessionId);

    /// <summary>
    /// Tells the launcher that an agent exited before it wrote its first report. The launcher may use it to fall
    /// back from the write-restricted token (ADR-011 §7 item 1, Q-9).
    /// </summary>
    void ReportEarlyExit();

    /// <summary>Tells the launcher that an agent wrote its first report (the current token works).</summary>
    void ReportWorking();
}

/// <summary>A running session agent with its pipes.</summary>
public interface IAgentProcess : IAsyncDisposable
{
    /// <summary>The agent's process ID.</summary>
    int ProcessId { get; }

    /// <summary>Completes with the exit code when the agent has exited.</summary>
    Task<int> Exited { get; }

    /// <summary>Next line on stdout (a report), or <c>null</c> at the end of the stream.</summary>
    /// <exception cref="AgentProtocolException">The line violates the protocol limits.</exception>
    Task<string?> ReadReportLineAsync(CancellationToken ct);

    /// <summary>Next line on stderr (diagnostics, errors), or <c>null</c> at the end of the stream.</summary>
    /// <exception cref="AgentProtocolException">The line violates the protocol limits.</exception>
    Task<string?> ReadErrorLineAsync(CancellationToken ct);

    /// <summary>Writes one command line to the agent's stdin (US-005, ADR-011 amendment). Thread-safe.</summary>
    /// <exception cref="IOException">The pipe is broken (the agent exited).</exception>
    Task WriteLineAsync(string line, CancellationToken ct);

    /// <summary>Asks the agent to stop (closes its stdin) and ends it if it has not exited after 2 s.</summary>
    Task StopAsync();
}
