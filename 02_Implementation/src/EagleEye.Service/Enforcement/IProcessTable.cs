namespace EagleEye.Service.Enforcement;

/// <summary>One process of a process snapshot (ADR-013 §5).</summary>
/// <param name="Pid">The process ID.</param>
/// <param name="ParentPid">The parent's process ID (may be stale or reused; check creation times).</param>
/// <param name="Created">The creation time (FILETIME ticks).</param>
/// <param name="SessionId">The session.</param>
/// <param name="OwnerSid">The token user, or null if unknown.</param>
/// <param name="ImagePath">The full program path.</param>
public sealed record ProcessRow(int Pid, int ParentPid, long Created, int SessionId, string? OwnerSid, string ImagePath);

/// <summary>Reads the processes of a session (<c>CreateToolhelp32Snapshot</c> + process facts).</summary>
public interface IProcessTable
{
    /// <summary>The processes of the session whose facts could be read.</summary>
    IReadOnlyList<ProcessRow> Snapshot(int sessionId);
}
