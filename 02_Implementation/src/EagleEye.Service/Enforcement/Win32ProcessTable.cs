using System.Runtime.InteropServices;
using EagleEye.Service.Monitoring;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Process snapshot with <c>CreateToolhelp32Snapshot</c> (PID, parent PID), filtered to one session with
/// <c>ProcessIdToSessionId</c>, and the facts of each process from <see cref="IProcessInspector"/> (creation time, owner,
/// path). Processes whose facts cannot be read are left out. Thin Win32, verified manually.
/// </summary>
public sealed unsafe partial class Win32ProcessTable(IProcessInspector inspector) : IProcessTable
{
    private const uint SnapProcess = 0x2;
    private static readonly nint InvalidHandle = -1;

    /// <inheritdoc />
    public IReadOnlyList<ProcessRow> Snapshot(int sessionId)
    {
        var rows = new List<ProcessRow>();
        foreach (var (pid, parent) in Enumerate())
        {
            if (ProcessIdToSessionId(pid, out var session) && session == sessionId && inspector.Inspect((int)pid) is { } facts)
            {
                rows.Add(new ProcessRow(facts.ProcessId, (int)parent, facts.CreationTime, facts.SessionId, facts.OwnerSid, facts.ImagePath));
            }
        }

        return rows;
    }

    private static List<(uint Pid, uint Parent)> Enumerate()
    {
        var result = new List<(uint, uint)>();
        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle)
        {
            return result;
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)sizeof(ProcessEntry) };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
            {
                result.Add((entry.ProcessId, entry.ParentProcessId));
            }
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        public fixed char ExeFile[260];
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(nint snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(nint snapshot, ref ProcessEntry entry);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
