using System.Diagnostics;
using System.Globalization;
using System.Media;
using Microsoft.Win32;

namespace OverlaySpike;

/// <summary>Countdown, overlay upkeep, hotkeys, tray menu, session events and CPU log of the spike.</summary>
internal sealed partial class SpikeController : ApplicationContext
{
    private const int WarnSeconds = 300;
    private const int CriticalSeconds = 60;
    private const double WarnPulseSeconds = 3;
    private const double WarnPulseHz = 4;
    private const double CriticalPulseHz = 1;
    private const int MarginLogicalPx = 24;
    private static readonly TimeSpan FeedbackDuration = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> IgnoredForegroundClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    private readonly SpikeLog log;
    private readonly SpikeSettings settings;
    private readonly int ownPid = Environment.ProcessId;
    private readonly Stopwatch runClock = Stopwatch.StartNew();
    private readonly Stopwatch countdownClock = new();
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly HotkeyWindow hotkeys;
    private readonly NotifyIcon tray;
    private readonly List<string> failedHotkeys = [];
    private OverlayWindow overlay;
    private TimeSpan countdownBase;
    private Level level;
    private TimeSpan warnStartedAt = TimeSpan.MinValue;
    private string? feedback;
    private TimeSpan feedbackUntil;
    private bool hidden;
    private nint currentMonitor;
    private int currentDpi;
    private PillContent? lastContent;
    private Point lastLocation;
    private bool forceRender = true;
    private TimeSpan lastUpkeep = TimeSpan.FromSeconds(-10);
    private string lastCoverKey = "<initial>";
    private int reassertCount;
    private int foregroundChanges;
    private int markCount;
    private bool exiting;
    private bool errorShown;
    private TimeSpan lastCpuWall;
    private TimeSpan lastCpuTime;
    private double maxCpu;

    public SpikeController(SpikeLog log, SpikeSettings settings, List<string> notes)
    {
        this.log = log;
        this.settings = settings;
        LogStart(notes);
        overlay = CreateOverlay();
        hotkeys = new HotkeyWindow(OnHotkey);
        RegisterHotkeys();
        tray = CreateTray();
        currentMonitor = PickInitialMonitor();
        SetCountdown(TimeSpan.FromMinutes(settings.StartMinutes));
        if (!WindowProbe.InstallForegroundHook(OnForeground))
        {
            log.Write("ERROR", "SetWinEventHook failed");
        }

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        using (var self = Process.GetCurrentProcess())
        {
            lastCpuTime = self.TotalProcessorTime;
        }

        lastCpuWall = runClock.Elapsed;
        timer.Tick += (_, _) => Tick();
        Tick();
        overlay.Reassert();
        timer.Start();
        log.Write("START", "running");
    }

    private enum Level
    {
        Normal,
        Warn,
        Critical,
        Expired,
    }

    /// <summary>Unhandled exception: log, one balloon tip, exit.</summary>
    public void OnUnhandled(Exception ex)
    {
        log.Write("ERROR", ex.ToString().ReplaceLineEndings(" | "));
        if (errorShown)
        {
            return;
        }

        errorShown = true;
        try
        {
            tray.ShowBalloonTip(5000, "OverlaySpike", "Fehler – siehe Log. OverlaySpike wird beendet.", ToolTipIcon.Error);
        }
        catch (Exception balloonEx) when (balloonEx is InvalidOperationException or ObjectDisposedException)
        {
            log.Write("ERROR", "balloon failed: " + balloonEx.Message);
        }

        var exitTimer = new System.Windows.Forms.Timer { Interval = 6000 };
        exitTimer.Tick += (_, _) => Shutdown("END", "exit after error");
        exitTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            tray.Dispose();
            hotkeys.Dispose();
            overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LogStart(List<string> notes)
    {
        log.Write("START", $"OverlaySpike {SystemInfo.AppVersion()} (US-010 throwaway spike)");
        log.Write("START", $"log folder: {log.Folder}{(log.IsFallback ? " (fallback: exe folder not writable)" : string.Empty)}");
        log.Write("START", $"exe: {Environment.ProcessPath}");
        log.Write("START", SystemInfo.WindowsVersion());
        log.Write("START", SystemInfo.UserAndSession());
        log.Write("START", "settings: " + settings);
        foreach (var note in notes)
        {
            log.Write("START", note);
        }

        foreach (var line in SystemInfo.Monitors())
        {
            log.Write("START", line);
        }

        log.Write("START", SystemInfo.Adapters());
    }

    private OverlayWindow CreateOverlay()
    {
        var window = new OverlayWindow(settings.Uniform);
        window.DpiMessage += (_, _) =>
        {
            log.Write("MONITOR", $"WM_DPICHANGED (monitor dpi now {Native.MonitorDpi(currentMonitor)})");
            forceRender = true;
            Render();
        };
        window.Show();
        forceRender = true;
        return window;
    }

    private nint PickInitialMonitor()
    {
        var fg = Native.GetForegroundWindow();
        if (fg != 0 && !IsIgnoredForeground(fg))
        {
            return Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST);
        }

        return Native.MonitorFromPoint(default, Native.MONITOR_DEFAULTTOPRIMARY);
    }

    private void SetCountdown(TimeSpan remaining)
    {
        countdownBase = remaining;
        countdownClock.Restart();
        level = LevelFor(DisplaySeconds());
        warnStartedAt = TimeSpan.MinValue;
        forceRender = true;
    }

    private int DisplaySeconds()
    {
        var remaining = countdownBase - countdownClock.Elapsed;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private static Level LevelFor(int seconds) => seconds switch
    {
        <= 0 => Level.Expired,
        <= CriticalSeconds => Level.Critical,
        <= WarnSeconds => Level.Warn,
        _ => Level.Normal,
    };

    private void Tick()
    {
        var seconds = DisplaySeconds();
        var newLevel = LevelFor(seconds);
        if (newLevel > level)
        {
            level = newLevel;
            OnLevelReached(newLevel);
        }

        if (feedback is not null && runClock.Elapsed >= feedbackUntil)
        {
            feedback = null;
        }

        Render();
        if (runClock.Elapsed - lastUpkeep >= TimeSpan.FromSeconds(1))
        {
            Upkeep(Native.GetForegroundWindow());
        }

        if (runClock.Elapsed - lastCpuWall >= TimeSpan.FromSeconds(settings.CpuLogSeconds))
        {
            LogCpu();
        }

        timer.Interval = NextInterval();
    }

    private void OnLevelReached(Level reached)
    {
        if (reached == Level.Warn)
        {
            warnStartedAt = runClock.Elapsed;
        }

        var colour = reached == Level.Warn ? "yellow" : "red";
        log.Write("LEVEL", $"{reached} at {FormatTime(DisplaySeconds())}: colour={colour}, beep={(settings.Beep ? "yes" : "no")}");
        if (settings.Beep)
        {
            SystemSounds.Exclamation.Play();
        }
    }

    private bool IsPulsing() =>
        level == Level.Critical || (level == Level.Warn && warnStartedAt != TimeSpan.MinValue && runClock.Elapsed - warnStartedAt < TimeSpan.FromSeconds(WarnPulseSeconds));

    private int NextInterval()
    {
        if (IsPulsing())
        {
            return 100;
        }

        var remainingMs = (countdownBase - countdownClock.Elapsed).TotalMilliseconds;
        var next = remainingMs > 0 ? (remainingMs % 1000) + 20 : 1000;
        if (feedback is not null)
        {
            next = Math.Min(next, (feedbackUntil - runClock.Elapsed).TotalMilliseconds + 10);
        }

        return (int)Math.Clamp(next, 15, 1000);
    }

    private PillContent BuildContent()
    {
        var baseAlpha = settings.Opacity * 255 / 100;
        double alpha = baseAlpha;
        if (IsPulsing())
        {
            var hz = level == Level.Warn ? WarnPulseHz : CriticalPulseHz;
            var phase = (Math.Sin(2 * Math.PI * hz * runClock.Elapsed.TotalSeconds) + 1) / 2;
            var high = Math.Max(baseAlpha, 230);
            alpha = (baseAlpha * 0.25) + ((high - (baseAlpha * 0.25)) * phase);
        }

        var colour = level switch
        {
            Level.Normal => Color.White,
            Level.Warn => Color.FromArgb(255, 214, 0),
            _ => Color.FromArgb(255, 70, 70),
        };
        return new PillContent($"{settings.Label} {FormatTime(DisplaySeconds())}", feedback, colour, (byte)Math.Clamp(alpha, 0, 255), settings.FontSizePt);
    }

    private static string FormatTime(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    private void Render()
    {
        if (hidden)
        {
            return;
        }

        var content = BuildContent();
        var dpi = Native.MonitorDpi(currentMonitor);
        var (bounds, _, _) = Native.MonitorInfo(currentMonitor);
        if (!forceRender && content == lastContent && dpi == currentDpi)
        {
            return;
        }

        if (currentDpi != 0 && dpi != currentDpi)
        {
            log.Write("MONITOR", $"dpi {currentDpi} -> {dpi} on {Native.MonitorInfo(currentMonitor).Device}");
        }

        using var bmp = PillRenderer.Render(content, dpi, overlay.IsUniform);
        var margin = MarginLogicalPx * dpi / 96;
        var x = settings.Corner is Corner.TopLeft or Corner.BottomLeft ? bounds.Left + margin : bounds.Right - margin - bmp.Width;
        var y = settings.Corner is Corner.TopLeft or Corner.TopRight ? bounds.Top + margin : bounds.Bottom - margin - bmp.Height;
        lastLocation = new Point(x, y);
        overlay.Present(bmp, lastLocation, overlay.IsUniform ? content.BackgroundAlpha : (byte)255);
        lastContent = content;
        currentDpi = dpi;
        forceRender = false;
    }

    private void OnForeground(nint hwnd)
    {
        if (exiting)
        {
            return;
        }

        foregroundChanges++;
        LogForeground(hwnd);
        Upkeep(hwnd);
    }

    private void LogForeground(nint hwnd)
    {
        if (hwnd == 0)
        {
            log.Write("FG", "none (0)");
            return;
        }

        var pid = WindowProbe.ProcessId(hwnd);
        var name = WindowProbe.ProcessName(pid, refresh: true);
        var frame = Native.FrameRect(hwnd);
        var monitor = Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST);
        var (bounds, device, _) = Native.MonitorInfo(monitor);
        var style = (long)Native.GetWindowLongPtrW(hwnd, Native.GWL_STYLE);
        var exStyle = (long)Native.GetWindowLongPtrW(hwnd, Native.GWL_EXSTYLE);
        var (quns, qunsName) = WindowProbe.NotificationState();
        var mode = WindowProbe.ModeGuess(hwnd, frame, bounds, style, quns);
        var own = pid == ownPid ? " [own process: tray menu/notify window]" : string.Empty;
        log.Write("FG", $"#{foregroundChanges} {name} pid={pid} class={Native.ClassName(hwnd)} title=\"{Native.Title(hwnd, 40)}\" " +
            $"rect={frame} monitor={device} {bounds} dpi={Native.MonitorDpi(monitor)} style=0x{style:X8} exstyle=0x{exStyle:X8} " +
            $"zoomed={Native.IsZoomed(hwnd)} quns={qunsName}({quns}) mode={mode}{own}");
    }

    private void Upkeep(nint foreground)
    {
        lastUpkeep = runClock.Elapsed;
        FollowMonitor(foreground);
        if (hidden)
        {
            return;
        }

        var coverer = FindCoverer();
        var key = coverer?.Key ?? string.Empty;
        if (key != lastCoverKey)
        {
            log.Write("COVER", coverer is null ? "not covered" : $"covered by {coverer.Value.Description}");
            lastCoverKey = key;
        }

        if (!settings.Reassert)
        {
            return;
        }

        overlay.Reassert();
        if (coverer is not null)
        {
            reassertCount++;
            log.Write("REASSERT", $"above {coverer.Value.Description}; effective re-asserts={reassertCount}");
        }
    }

    private void FollowMonitor(nint foreground)
    {
        if (foreground == 0 || IsIgnoredForeground(foreground))
        {
            return;
        }

        var monitor = Native.MonitorFromWindow(foreground, Native.MONITOR_DEFAULTTONEAREST);
        if (monitor == currentMonitor || monitor == 0)
        {
            return;
        }

        var (oldBounds, oldDevice, _) = Native.MonitorInfo(currentMonitor);
        var (newBounds, newDevice, _) = Native.MonitorInfo(monitor);
        log.Write("MONITOR", $"follow foreground: {oldDevice} {oldBounds} dpi={Native.MonitorDpi(currentMonitor)} -> {newDevice} {newBounds} dpi={Native.MonitorDpi(monitor)}");
        currentMonitor = monitor;
        forceRender = true;
        Render();
    }

    private bool IsIgnoredForeground(nint hwnd) =>
        WindowProbe.ProcessId(hwnd) == ownPid || IgnoredForegroundClasses.Contains(Native.ClassName(hwnd));

    private (string Key, string Description)? FindCoverer()
    {
        if (!Native.GetWindowRect(overlay.Handle, out var mine))
        {
            return null;
        }

        var h = Native.GetWindow(overlay.Handle, Native.GW_HWNDPREV);
        for (var guard = 0; h != 0 && guard < 10_000; guard++)
        {
            if (Native.IsWindowVisible(h) && Native.Cloaked(h) == 0 && Native.GetWindowRect(h, out var r) && r.Intersects(mine))
            {
                var pid = WindowProbe.ProcessId(h);
                var exStyle = (long)Native.GetWindowLongPtrW(h, Native.GWL_EXSTYLE);
                var flags = new List<string>();
                if ((exStyle & Native.WS_EX_TOPMOST) != 0)
                {
                    flags.Add("topmost");
                }

                if ((exStyle & Native.WS_EX_LAYERED) != 0)
                {
                    flags.Add("layered");
                }

                if ((exStyle & Native.WS_EX_TRANSPARENT) != 0)
                {
                    flags.Add("transparent");
                }

                var cls = Native.ClassName(h);
                var name = WindowProbe.ProcessName(pid);
                return ($"{h}|{pid}|{cls}", $"{name} {cls} hwnd=0x{h:X} rect={r} [{string.Join(",", flags)}]");
            }

            h = Native.GetWindow(h, Native.GW_HWNDPREV);
        }

        return null;
    }

    private void LogCpu()
    {
        using var self = Process.GetCurrentProcess();
        var wall = runClock.Elapsed;
        var cpuTime = self.TotalProcessorTime;
        var cpu = (cpuTime - lastCpuTime).TotalMilliseconds / (wall - lastCpuWall).TotalMilliseconds / Environment.ProcessorCount * 100;
        maxCpu = Math.Max(maxCpu, cpu);
        lastCpuTime = cpuTime;
        lastCpuWall = wall;
        var gdi = Native.GetGuiResources(-1, 0);
        var user = Native.GetGuiResources(-1, 1);
        log.Write("CPU", string.Create(CultureInfo.InvariantCulture, $"cpu={cpu:0.000}% workingSet={self.WorkingSet64 / 1048576.0:0.0}MB privateBytes={self.PrivateMemorySize64 / 1048576.0:0.0}MB gdi={gdi} user={user} handles={self.HandleCount}"));
    }

    private string Summary()
    {
        using var self = Process.GetCurrentProcess();
        return string.Create(CultureInfo.InvariantCulture, $"run time={runClock.Elapsed:hh\\:mm\\:ss}, effective re-asserts={reassertCount}, foreground changes={foregroundChanges}, " +
            $"max CPU={maxCpu:0.000}%, peak working set={self.PeakWorkingSet64 / 1048576.0:0.0}MB, markers={markCount}");
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        log.Write("SESSION", e.Reason.ToString());
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
        {
            overlay.Reassert();
            forceRender = true;
            Render();
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        e.Cancel = false;
        Shutdown("SESSION", $"session ending ({e.Reason}); {Summary()}");
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        log.Write("MONITOR", "display settings changed");
        foreach (var line in SystemInfo.Monitors())
        {
            log.Write("MONITOR", line);
        }

        currentMonitor = PickInitialMonitor();
        forceRender = true;
        Render();
    }

    private void Shutdown(string category, string message)
    {
        if (exiting)
        {
            return;
        }

        exiting = true;
        timer.Stop();
        WindowProbe.RemoveForegroundHook();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.SessionEnding -= OnSessionEnding;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        log.Write(category, message);
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }
}
