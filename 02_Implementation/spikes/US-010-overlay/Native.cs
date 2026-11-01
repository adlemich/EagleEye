using System.Runtime.InteropServices;

namespace OverlaySpike;

/// <summary>Win32 interop for the spike. No SetWindowsHookEx, no OpenProcess, no registry writes.</summary>
internal static unsafe partial class Native
{
    public const int WS_EX_TOPMOST = 0x8;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_APPWINDOW = 0x40000;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_NOACTIVATE = 0x8000000;
    public const long WS_CAPTION = 0xC00000;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const uint GW_HWNDPREV = 3;
    public const uint GW_OWNER = 4;
    public static readonly nint HWND_TOPMOST = -1;
    public static readonly nint HWND_MESSAGE = -3;
    public const uint SWP_NOSIZE = 0x1;
    public const uint SWP_NOMOVE = 0x2;
    public const uint SWP_NOZORDER = 0x4;
    public const uint SWP_NOACTIVATE = 0x10;
    public const uint SWP_NOOWNERZORDER = 0x200;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint ULW_ALPHA = 0x2;
    public const uint LWA_ALPHA = 0x2;
    public const byte AC_SRC_OVER = 0;
    public const byte AC_SRC_ALPHA = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint EVENT_SYSTEM_FOREGROUND = 3;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_MOUSEACTIVATE = 0x21;
    public const int WM_NCHITTEST = 0x84;
    public const int WM_HOTKEY = 0x312;
    public const int WM_DPICHANGED = 0x2E0;
    public const int MA_NOACTIVATE = 3;
    public const int HTTRANSPARENT = -1;
    public const int ENUM_CURRENT_SETTINGS = -1;
    public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;

        public readonly bool Intersects(RECT o) => Left < o.Right && o.Left < Right && Top < o.Bottom && o.Top < Bottom;

        public readonly bool Covers(RECT o) => Left <= o.Left && Top <= o.Top && Right >= o.Right && Bottom >= o.Bottom;

        public override readonly string ToString() => $"({Left},{Top} {Width}x{Height})";
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
    public struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
        public fixed char Device[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAY_DEVICE
    {
        public int Size;
        public fixed char DeviceName[32];
        public fixed char DeviceString[128];
        public uint StateFlags;
        public fixed char DeviceID[128];
        public fixed char DeviceKey[128];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVMODE
    {
        public fixed char DeviceName[32];
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int PositionX, PositionY;
        public uint DisplayOrientation, DisplayFixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        public fixed char FormName[32];
        public ushort LogPixels;
        public uint BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
        public uint IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }

    [LibraryImport("user32.dll")]
    public static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateLayeredWindow(nint hwnd, nint hdcDst, POINT* dst, SIZE* size, nint hdcSrc, POINT* src, uint key, BLENDFUNCTION* blend, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

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
    public static partial bool GetMonitorInfoW(nint monitor, MONITORINFOEX* info);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial nint GetWindow(nint hwnd, uint cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsZoomed(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    public static partial int GetClassNameW(nint hwnd, char* buffer, int max);

    [LibraryImport("user32.dll")]
    public static partial int GetWindowTextW(nint hwnd, char* buffer, int max);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hwnd, int attribute, void* value, int size);

    [LibraryImport("user32.dll")]
    public static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module, nint callback, uint pid, uint tid, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hwnd, int id);

    [LibraryImport("shell32.dll")]
    public static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll")]
    public static partial uint GetGuiResources(nint process, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayDevicesW(char* device, uint index, DISPLAY_DEVICE* info, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplaySettingsW(char* device, int mode, DEVMODE* info);

    /// <summary>Window class name (no message is sent to the window).</summary>
    public static string ClassName(nint hwnd)
    {
        var buffer = stackalloc char[257];
        var n = GetClassNameW(hwnd, buffer, 257);
        return n > 0 ? new string(buffer, 0, n) : string.Empty;
    }

    /// <summary>Window title, truncated.</summary>
    public static string Title(nint hwnd, int max)
    {
        var buffer = stackalloc char[256];
        var n = GetWindowTextW(hwnd, buffer, 256);
        var text = n > 0 ? new string(buffer, 0, n) : string.Empty;
        return text.Length > max ? text[..max] + "…" : text;
    }

    /// <summary>Visible frame rectangle (DWM extended frame bounds), or the window rectangle as fallback.</summary>
    public static RECT FrameRect(nint hwnd)
    {
        RECT rect;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(RECT)) == 0)
        {
            return rect;
        }

        _ = GetWindowRect(hwnd, out rect);
        return rect;
    }

    /// <summary>DWM cloaked state (0 = not cloaked).</summary>
    public static int Cloaked(nint hwnd)
    {
        int cloaked = 0;
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &cloaked, sizeof(int)) == 0 ? cloaked : 0;
    }

    /// <summary>Monitor bounds (physical pixels, the process is PerMonitorV2) and device name.</summary>
    public static (RECT Bounds, string Device, bool Primary) MonitorInfo(nint monitor)
    {
        MONITORINFOEX info = default;
        info.Size = sizeof(MONITORINFOEX);
        if (!GetMonitorInfoW(monitor, &info))
        {
            return (default, "?", false);
        }

        return (info.Monitor, new string(info.Device), (info.Flags & 1) != 0);
    }

    /// <summary>Effective DPI of a monitor (96 if unknown).</summary>
    public static int MonitorDpi(nint monitor) =>
        GetDpiForMonitor(monitor, 0, out var x, out _) == 0 && x > 0 ? (int)x : 96;

    /// <summary>Current refresh rate of a display device, 0 if unknown.</summary>
    public static int RefreshRate(string device)
    {
        DEVMODE mode = default;
        mode.Size = (ushort)sizeof(DEVMODE);
        fixed (char* name = device)
        {
            return EnumDisplaySettingsW(name, ENUM_CURRENT_SETTINGS, &mode) ? (int)mode.DisplayFrequency : 0;
        }
    }

    /// <summary>Names of the display adapters attached to the desktop.</summary>
    public static List<string> DisplayAdapters()
    {
        var attached = new List<string>();
        var all = new List<string>();
        for (uint i = 0; i < 32; i++)
        {
            DISPLAY_DEVICE device = default;
            device.Size = sizeof(DISPLAY_DEVICE);
            if (!EnumDisplayDevicesW(null, i, &device, 0))
            {
                break;
            }

            var name = new string(device.DeviceString);
            var target = (device.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0 ? attached : all;
            if (!target.Contains(name))
            {
                target.Add(name);
            }
        }

        return attached.Count > 0 ? attached : all.ConvertAll(n => n + " (not attached to desktop)");
    }
}
