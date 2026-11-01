namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// The out-of-context foreground WinEvent hook (US-010 spike S-6, ADR-022 §2): <c>EVENT_SYSTEM_FOREGROUND</c> with
/// <c>WINEVENT_OUTOFCONTEXT</c>, so no DLL is loaded into other processes. The callback runs on the thread that installed
/// the hook (the UI thread with the WinForms message loop). One hook per process. Verified manually.
/// </summary>
internal static unsafe class ForegroundHook
{
    private static Action<nint>? s_handler;
    private static nint s_hook;

    /// <summary>Installs the hook; returns whether it was installed.</summary>
    public static bool Install(Action<nint> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        s_handler = handler;
        delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void> callback = &OnWinEvent;
        s_hook = OverlayNative.SetWinEventHook(
            OverlayNative.EVENT_SYSTEM_FOREGROUND, OverlayNative.EVENT_SYSTEM_FOREGROUND, 0, (nint)callback, 0, 0, OverlayNative.WINEVENT_OUTOFCONTEXT);
        return s_hook != 0;
    }

    /// <summary>Removes the hook (no-op if not installed).</summary>
    public static void Remove()
    {
        if (s_hook != 0)
        {
            OverlayNative.UnhookWinEvent(s_hook);
            s_hook = 0;
        }

        s_handler = null;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        try
        {
            s_handler?.Invoke(hwnd);
        }
        catch (Exception)
        {
            // An exception must never unwind into native code; the next foreground change or the 1-s upkeep follows.
        }
    }
}
