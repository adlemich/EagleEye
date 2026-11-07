using System.Runtime.InteropServices;

namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// Win32 interop of the overlay (US-010 Decision 4, spike S-1 to S-9). No process handles, no window hooks in other
/// processes (the WinEvent hook is out of context), no messages to other windows (ADR-022 §3). Verified manually.
/// </summary>
internal static unsafe partial class OverlayNative
{
    public const int WM_MOUSEACTIVATE = 0x21;
    public const int WM_NCHITTEST = 0x84;
    public const int WM_DPICHANGED = 0x2E0;
    public const int MA_NOACTIVATE = 3;
    public const int HTTRANSPARENT = -1;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint SWP_NOSIZE = 0x1;
    public const uint SWP_NOMOVE = 0x2;
    public const uint SWP_NOACTIVATE = 0x10;
    public const uint SWP_NOOWNERZORDER = 0x200;
    public const uint ULW_ALPHA = 0x2;
    public const byte AC_SRC_OVER = 0;
    public const byte AC_SRC_ALPHA = 1;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint EVENT_SYSTEM_FOREGROUND = 3;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const int DefaultDpi = 96;
    public static readonly nint HWND_TOPMOST = -1;

    private const int ClassNameCapacity = 257;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int Cx, Cy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateLayeredWindow(nint hwnd, nint hdcDst, POINT* dst, SIZE* size, nint hdcSrc, POINT* src, uint key, BLENDFUNCTION* blend, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfoW(nint monitor, MONITORINFO* info);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    public static partial int GetClassNameW(nint hwnd, char* buffer, int max);

    [LibraryImport("user32.dll")]
    public static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module, nint callback, uint pid, uint tid, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint hook);

    /// <summary>The window's class name (no message is sent to the window).</summary>
    public static string ClassName(nint hwnd)
    {
        var buffer = stackalloc char[ClassNameCapacity];
        var length = GetClassNameW(hwnd, buffer, ClassNameCapacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    /// <summary>The process id of the window's thread (no process handle is opened).</summary>
    public static uint ProcessIdOf(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    /// <summary>The monitor bounds in physical pixels (the process is PerMonitorV2), or an empty rectangle.</summary>
    public static Rectangle MonitorBounds(nint monitor)
    {
        MONITORINFO info = default;
        info.Size = sizeof(MONITORINFO);
        return GetMonitorInfoW(monitor, &info)
            ? Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom)
            : Rectangle.Empty;
    }

    /// <summary>The effective DPI of a monitor (<see cref="DefaultDpi"/> if unknown).</summary>
    public static int MonitorDpi(nint monitor) =>
        GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var x, out _) == 0 && x > 0 ? (int)x : DefaultDpi;
}
