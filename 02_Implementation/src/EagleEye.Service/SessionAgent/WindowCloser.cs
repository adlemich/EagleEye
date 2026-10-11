using System.Runtime.InteropServices;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Executes a close command in the agent (ADR-013 §4, ADR-011 amendment, coding guidelines §12.4): verifies each
/// target's creation time with <c>OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)</c> + <c>GetProcessTimes</c>,
/// re-enumerates the windows, selects the app windows of the verified targets (<see cref="WindowCloseSelection"/>)
/// and posts <c>WM_CLOSE</c> with <c>PostMessageW</c> — the only message call the agent makes. <c>PostMessage</c> is
/// asynchronous, so a hung app cannot block the agent. Never terminates anything. Thin Win32, verified manually.
/// </summary>
public sealed partial class WindowCloser(WindowEnumerator enumerator)
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint WmClose = 0x0010;

    /// <summary>Posts <c>WM_CLOSE</c> to the app windows of the targets and returns the answer.</summary>
    public CloseAnswer Close(CloseCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var verified = command.Targets.Where(t => CreationTime(t.Pid) == t.Created).Select(t => t.Pid).ToHashSet();
        var (windows, _) = enumerator.Enumerate();
        var posted = 0;
        var reached = new HashSet<int>();
        foreach (var (pid, window) in WindowCloseSelection.Select(verified, windows))
        {
            if (PostMessageW(window, WmClose, 0, 0))
            {
                posted++;
                reached.Add(pid);
            }
        }

        return new CloseAnswer(command.Id, posted, command.Targets.Count(t => !reached.Contains(t.Pid)));
    }

    private static long? CreationTime(int pid)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
        if (process == 0)
        {
            return null;
        }

        try
        {
            return GetProcessTimes(process, out var created, out _, out _, out _) ? created : null;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(nint process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
