using System.Runtime.InteropServices;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Reads the top-level windows of the agent's desktop (ADR-011 §4, §7 items 8 and 9). Security rules: only
/// window functions that do not send messages to the target window (<c>EnumWindows</c>, <c>EnumChildWindows</c>,
/// <c>IsWindowVisible</c>, <c>GetWindowRect</c>, <c>GetWindow(GW_OWNER)</c>, <c>GetWindowLongPtr(GWL_EXSTYLE)</c>,
/// <c>GetClassName</c>, <c>GetWindowThreadProcessId</c>, <c>DwmGetWindowAttribute(DWMWA_CLOAKED)</c>); never
/// <c>GetWindowText</c>, <c>SendMessage*</c> or <c>PostMessage*</c>; no window, hook, COM, shell, UIA or DPI API.
/// The process name of a visible window is read with <c>QueryFullProcessImageName</c> (needed to recognise
/// <c>explorer.exe</c>). At most <see cref="MaxWindows"/> windows per scan. Thin Win32, verified manually.
/// </summary>
public sealed unsafe partial class WindowEnumerator
{
    /// <summary>Maximum number of top-level windows read per scan (ADR-011 §7 item 9).</summary>
    public const int MaxWindows = 20_000;

    private const int MaxChildWindows = 1_000;
    private const int ClassNameCapacity = 257;
    private const uint GwOwner = 4;
    private const int GwlExStyle = -20;
    private const uint DwmwaCloaked = 14;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    /// <summary>Reads all top-level windows; <c>Truncated</c> is true when the limit was reached.</summary>
    public (IReadOnlyList<WindowInfo> Windows, bool Truncated) Enumerate()
    {
        var topLevel = Collect(0, MaxWindows, out var truncated);
        var names = new Dictionary<int, string>();
        var windows = new List<WindowInfo>(topLevel.Count);
        foreach (var handle in topLevel)
        {
            windows.Add(Read(handle, names));
        }

        return (windows, truncated);
    }

    private static WindowInfo Read(nint handle, Dictionary<int, string> names)
    {
        var visible = IsWindowVisible(handle);
        _ = GetWindowRect(handle, out var rect);
        _ = GetWindowThreadProcessId(handle, out var pid);
        var className = GetClass(handle);
        var cloaked = DwmGetWindowAttribute(handle, DwmwaCloaked, out var cloakValue, sizeof(int)) == 0 ? cloakValue : 0;
        var processName = string.Empty;
        if (visible && !names.TryGetValue((int)pid, out processName))
        {
            processName = ProcessName(pid);
            names[(int)pid] = processName;
        }

        int? hosted = visible && className == AppWindowRule.StoreFrameClass ? FindHostedProcess(handle, (int)pid) : null;
        return new WindowInfo(
            handle,
            (int)pid,
            className,
            visible,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top,
            GetWindow(handle, GwOwner) != 0,
            GetWindowLongPtrW(handle, GwlExStyle),
            cloaked,
            hosted,
            processName);
    }

    private static int? FindHostedProcess(nint frame, int framePid)
    {
        foreach (var child in Collect(frame, MaxChildWindows, out _))
        {
            _ = GetWindowThreadProcessId(child, out var childPid);
            if ((int)childPid != framePid && GetClass(child) == CoreWindowClass)
            {
                return (int)childPid;
            }
        }

        return null;
    }

    private static List<nint> Collect(nint parent, int limit, out bool truncated)
    {
        var state = new CollectState(limit);
        var handle = GCHandle.Alloc(state);
        try
        {
            if (parent == 0)
            {
                _ = EnumWindows(&OnWindow, GCHandle.ToIntPtr(handle));
            }
            else
            {
                _ = EnumChildWindows(parent, &OnWindow, GCHandle.ToIntPtr(handle));
            }
        }
        finally
        {
            handle.Free();
        }

        truncated = state.Truncated;
        return state.Handles;
    }

    [UnmanagedCallersOnly]
    private static int OnWindow(nint window, nint parameter)
    {
        var state = (CollectState)GCHandle.FromIntPtr(parameter).Target!; // Allocated in Collect, never null.
        if (state.Handles.Count >= state.Limit)
        {
            state.Truncated = true;
            return 0;
        }

        state.Handles.Add(window);
        return 1;
    }

    private static string GetClass(nint window)
    {
        var buffer = stackalloc char[ClassNameCapacity];
        var length = GetClassNameW(window, buffer, ClassNameCapacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private static string ProcessName(uint pid)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process == 0)
        {
            return string.Empty;
        }

        try
        {
            var buffer = stackalloc char[1024];
            var size = 1024u;
            return QueryFullProcessImageNameW(process, 0, buffer, ref size) ? Path.GetFileName(new string(buffer, 0, (int)size)) : string.Empty;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    private sealed class CollectState(int limit)
    {
        public int Limit { get; } = limit;

        public List<nint> Handles { get; } = [];

        public bool Truncated { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumChildWindows(nint parent, delegate* unmanaged<nint, nint, int> callback, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint window, uint command);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint window, int index);

    [LibraryImport("user32.dll")]
    private static partial int GetClassNameW(nint window, char* className, int maxCount);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint window, uint attribute, out int value, uint size);

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageNameW(nint process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
