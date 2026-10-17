using EagleEye.Service.Monitoring;
using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>The means of a close sequence: the agent's graceful close and the service's process handles (ADR-013 §4, §5).</summary>
public interface IAppCloser
{
    /// <summary>Asks the session agent to post <c>WM_CLOSE</c> to the targets' app windows; <c>null</c> if not answered.</summary>
    Task<CloseAnswer?> RequestGracefulCloseAsync(int sessionId, IReadOnlyList<ObservedProcess> targets, CancellationToken ct);

    /// <summary>Computes the kill set from a fresh process snapshot and opens a handle to every member still running.</summary>
    IReadOnlyList<IProcessHandle> OpenKillSet(CloseRequest start, IReadOnlySet<(int Pid, long Created)> extraRoots);

    /// <summary>Opens one process (creation time verified), or <c>null</c>.</summary>
    IProcessHandle? Open(ObservedProcess process);
}

/// <summary>
/// Glue between a close sequence and the agent, the process table and the terminator: the kill set excludes the
/// enforcement ignore list, EagleEye's own programs (by installation path) and the account's other open apps
/// (<see cref="OpenAppsView"/>, without the closed app's own path, US-006 TI-9).
/// </summary>
public sealed class AppCloser(
    ISessionAgentSupervisor supervisor,
    IProcessTable processTable,
    IProcessTerminator terminator,
    EnforcementIgnoreList ignoreList,
    ProgramPathPolicy pathPolicy,
    OpenAppsView openApps) : IAppCloser
{
    /// <inheritdoc />
    public Task<CloseAnswer?> RequestGracefulCloseAsync(int sessionId, IReadOnlyList<ObservedProcess> targets, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return supervisor.RequestCloseAsync(sessionId, [.. targets.Select(t => new CloseTarget(t.Pid, t.Created))], ct);
    }

    /// <inheritdoc />
    public IReadOnlyList<IProcessHandle> OpenKillSet(CloseRequest start, IReadOnlySet<(int Pid, long Created)> extraRoots)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(extraRoots);
        // TI-9: the app being closed may itself be open (running-app close); only the *other* open apps are excluded.
        var otherOpen = openApps.Of(start.AccountSid).Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p, start.ProgramPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var request = new KillSetRequest(start.SessionId, start.AccountSid, start.ProgramPath, extraRoots, otherOpen);
        var members = KillSetBuilder.Build(processTable.Snapshot(start.SessionId), request, IsExempt);
        var handles = new List<IProcessHandle>();
        foreach (var member in members)
        {
            if (terminator.Open(member.Pid, member.Created) is { } handle)
            {
                handles.Add(handle);
            }
        }

        return handles;
    }

    /// <inheritdoc />
    public IProcessHandle? Open(ObservedProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return terminator.Open(process.Pid, process.Created);
    }

    private bool IsExempt(string path) => ignoreList.IsIgnored(path) || pathPolicy.IsEagleEyeProgram(path);
}
