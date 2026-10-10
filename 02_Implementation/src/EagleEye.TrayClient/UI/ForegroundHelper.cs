using System.Runtime.InteropServices;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// Best-effort keyboard focus for the kid's message (ADR-014 §3, coding guidelines §12.2): Windows' foreground lock
/// lets a background process take the focus only in documented cases. After <c>Activate()</c>, if the window is not
/// the foreground window, one synthetic Alt press/release (<c>SendInput</c>) and <c>SetForegroundWindow</c>; if Windows
/// still refuses, the taskbar button flashes. All Win32 calls of the tray live here. Verified manually.
/// </summary>
internal static partial class ForegroundHelper
{
    private const ushort AltKey = 0x12;
    private const uint KeyUp = 0x0002;
    private const uint InputKeyboard = 1;
    private const uint FlashAll = 0x3;
    private const uint FlashTimerNoForeground = 0xC;

    /// <summary>Tries to bring the window to the foreground; returns whether it is the foreground window.</summary>
    public static bool BringToFront(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        form.Activate();
        if (GetForegroundWindow() == form.Handle)
        {
            return true;
        }

        PressAlt();
        _ = SetForegroundWindow(form.Handle);
        if (GetForegroundWindow() == form.Handle)
        {
            return true;
        }

        Flash(form.Handle);
        return false;
    }

    private static void PressAlt()
    {
        Input[] inputs =
        [
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = AltKey } },
            new Input { Type = InputKeyboard, Keyboard = new KeyboardInput { VirtualKey = AltKey, Flags = KeyUp } },
        ];
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void Flash(nint window)
    {
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = window,
            Flags = FlashAll | FlashTimerNoForeground,
        };
        _ = FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashInfo info);
}
