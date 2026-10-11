using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// <c>OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE)</c>, creation time checked with
/// <c>GetProcessTimes</c> on that handle, <c>WaitForSingleObject(0)</c> and <c>TerminateProcess</c> on the same handle
/// (ADR-013 §4, coding guidelines §12.4). The service runs as SYSTEM, which the default process DACL grants these
/// rights. Thin Win32, verified manually.
/// </summary>
public sealed partial class Win32ProcessTerminator : IProcessTerminator
{
    private const uint Synchronize = 0x0010_0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint ProcessTerminate = 0x0001;
    private const uint WaitObject0 = 0;
    private const uint TerminatedExitCode = 1;

    /// <inheritdoc />
    public IProcessHandle? Open(int pid, long created)
    {
        var handle = OpenProcess(Synchronize | ProcessQueryLimitedInformation | ProcessTerminate, false, (uint)pid);
        if (handle.IsInvalid || !GetProcessTimes(handle, out var actual, out _, out _, out _) || actual != created)
        {
            handle.Dispose();
            return null;
        }

        return new Handle(pid, handle);
    }

    private sealed class Handle(int pid, SafeProcessHandle handle) : IProcessHandle
    {
        public int Pid { get; } = pid;

        public bool HasExited => WaitForSingleObject(handle, 0) == WaitObject0;

        public int Terminate() => TerminateProcess(handle, TerminatedExitCode) ? 0 : Marshal.GetLastPInvokeError();

        public void Dispose() => handle.Dispose();
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
