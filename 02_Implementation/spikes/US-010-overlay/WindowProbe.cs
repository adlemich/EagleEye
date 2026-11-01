using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OverlaySpike;

/// <summary>
/// Reads other windows without touching their processes: process names come from the system process snapshot
/// (Process.GetProcesses), never from a process handle (no OpenProcess, no MainModule, no StartTime).
/// Also hosts the out-of-context foreground WinEvent hook (no DLL is loaded into other processes).
/// </summary>
internal static unsafe class WindowProbe
{
    private static readonly Dictionary<int, string> Names = [];
    private static Action<nint>? foregroundHandler;
    private static nint hook;

    /// <summary>Installs the EVENT_SYSTEM_FOREGROUND hook (WINEVENT_OUTOFCONTEXT).</summary>
    public static bool InstallForegroundHook(Action<nint> handler)
    {
        foregroundHandler = handler;
        delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void> callback = &OnWinEvent;
        hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, 0, (nint)callback, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        return hook != 0;
    }

    public static void RemoveForegroundHook()
    {
        if (hook != 0)
        {
            Native.UnhookWinEvent(hook);
            hook = 0;
        }
    }

    /// <summary>Process name for a PID from the system snapshot; refreshes the snapshot on a miss or when asked.</summary>
    public static string ProcessName(int pid, bool refresh = false)
    {
        if (!refresh && Names.TryGetValue(pid, out var name))
        {
            return name;
        }

        Names.Clear();
        foreach (var p in Process.GetProcesses())
        {
            Names[p.Id] = p.ProcessName;
            p.Dispose();
        }

        return Names.TryGetValue(pid, out name) ? name : "?";
    }

    public static int ProcessId(nint hwnd)
    {
        _ = Native.GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    /// <summary>Mode guess for the foreground window (heuristic; the photo is the truth).</summary>
    public static string ModeGuess(nint hwnd, Native.RECT frame, Native.RECT monitor, long style, int quns)
    {
        const int QunsRunningD3dFullScreen = 3;
        if (quns == QunsRunningD3dFullScreen)
        {
            return "exclusive?";
        }

        if (frame.Covers(monitor) && (style & Native.WS_CAPTION) != Native.WS_CAPTION)
        {
            return "fullscreen-borderless";
        }

        if (Native.IsZoomed(hwnd))
        {
            return "maximized";
        }

        return frame.Covers(monitor) ? "windowed(covers-monitor,caption)" : "windowed";
    }

    public static (int State, string Name) NotificationState()
    {
        if (Native.SHQueryUserNotificationState(out var state) != 0)
        {
            return (0, "error");
        }

        var name = state switch
        {
            1 => "NOT_PRESENT",
            2 => "BUSY",
            3 => "RUNNING_D3D_FULL_SCREEN",
            4 => "PRESENTATION_MODE",
            5 => "ACCEPTS_NOTIFICATIONS",
            6 => "QUIET_TIME",
            7 => "APP",
            _ => "unknown",
        };
        return (state, name);
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hookHandle, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            foregroundHandler?.Invoke(hwnd);
        }
        catch (Exception ex)
        {
            Program.ReportError(ex);
        }
    }
}
