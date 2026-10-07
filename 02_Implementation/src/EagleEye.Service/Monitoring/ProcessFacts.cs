namespace EagleEye.Service.Monitoring;

/// <summary>What the service reads about a process reported by an agent.</summary>
/// <param name="ProcessId">The process ID.</param>
/// <param name="SessionId">The session the process runs in.</param>
/// <param name="OwnerSid">The SID of the process token's user, or null if unknown.</param>
/// <param name="CreationTime">Creation time (FILETIME ticks); with the PID the identity of the process.</param>
/// <param name="ImagePath">The full path of the program file (<c>QueryFullProcessImageName</c>).</param>
/// <param name="PackageFullName">The package full name of a packaged (Store) app, or null.</param>
public sealed record ProcessFacts(int ProcessId, int SessionId, string? OwnerSid, long CreationTime, string ImagePath, string? PackageFullName);

/// <summary>Reads process facts across sessions.</summary>
public interface IProcessInspector
{
    /// <summary>The creation time of the process (cheap; detects PID reuse), or null if it is gone or inaccessible.</summary>
    long? GetCreationTime(int processId);

    /// <summary>All facts of the process, or null if it is gone or inaccessible.</summary>
    ProcessFacts? Inspect(int processId);
}
