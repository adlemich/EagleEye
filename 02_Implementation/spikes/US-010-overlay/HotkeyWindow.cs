namespace OverlaySpike;

/// <summary>Message-only window (never visible, not enumerated by EnumWindows) that receives the global hotkeys.</summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const uint Modifiers = Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT | Native.MOD_NOREPEAT;
    private readonly Action<int> onHotkey;
    private readonly List<int> registered = [];

    public HotkeyWindow(Action<int> onHotkey)
    {
        this.onHotkey = onHotkey;
        CreateHandle(new CreateParams { Caption = "OverlaySpikeHotkeys", Parent = Native.HWND_MESSAGE });
    }

    /// <summary>Registers Ctrl+Alt+Shift+key; returns the Win32 error code, 0 on success.</summary>
    public int Register(int id, Keys key)
    {
        if (Native.RegisterHotKey(Handle, id, Modifiers, (uint)key))
        {
            registered.Add(id);
            return 0;
        }

        return System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
    }

    public void Dispose()
    {
        foreach (var id in registered)
        {
            Native.UnregisterHotKey(Handle, id);
        }

        registered.Clear();
        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            onHotkey((int)m.WParam);
        }

        base.WndProc(ref m);
    }
}
