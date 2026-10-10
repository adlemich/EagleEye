namespace EagleEye.Service.Enforcement;

/// <summary>
/// Opens kid processes for waiting and termination (service only, coding guidelines §12.4): by PID, with the
/// creation time verified on the opened handle, so a reused PID is never touched.
/// </summary>
public interface IProcessTerminator
{
    /// <summary>The process, or <c>null</c> if it is gone, inaccessible or the PID now belongs to another process.</summary>
    IProcessHandle? Open(int pid, long created);
}

/// <summary>An opened process (<c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE</c>).</summary>
public interface IProcessHandle : IDisposable
{
    /// <summary>The process ID.</summary>
    int Pid { get; }

    /// <summary>Whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Terminates the process through this handle; returns 0 or the Win32 error.</summary>
    int Terminate();
}
