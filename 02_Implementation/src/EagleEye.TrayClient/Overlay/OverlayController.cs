using System.Globalization;
using System.Media;
using EagleEye.Shared.Models;
using Microsoft.Win32;

namespace EagleEye.TrayClient.Overlay;

/// <summary>
/// Runs the remaining-time overlay of the tray client (US-010 Decisions 4 to 8, ADR-022): receives the service's model,
/// counts down (<see cref="RemainingTimeClock"/>), lays out (<see cref="OverlayLayout"/>), animates (<see cref="PillAnimation"/>),
/// renders (<see cref="PillRenderer"/>) only when something changed, places the pill in the chosen corner of the foreground
/// window's monitor and keeps it top-most (foreground hook and at most once per second). Hides on disconnect, after 15 s
/// without a push and while the session is locked. UI thread only; verified manually (Step 5).
/// </summary>
internal sealed class OverlayController : IDisposable
{
    private const int MarginLogicalPx = 24;
    private const int PulseIntervalMs = 100;
    private const int IdleIntervalMs = 1000;
    private static readonly TimeSpan UpkeepInterval = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> IgnoredForegroundClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    private readonly OverlaySettingsModel _settings;
    private readonly TimeProvider _time;
    private readonly RemainingTimeClock _clock;
    private readonly PillRenderer _renderer = new();
    private readonly OverlayWindow _window = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly long _started;
    private long? _quickPulseStarted;
    private long _lastUpkeep;
    private nint _monitor;
    private string? _lastKey;
    private bool _visible;

    /// <summary>Creates the hidden overlay window and starts following foreground, session and display changes.</summary>
    public OverlayController(OverlaySettingsModel settings, TimeProvider time)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _clock = new RemainingTimeClock(time);
        _started = time.GetTimestamp();
        _window.DpiChangedMessage += (_, _) => Render(force: true);
        _window.Show();
        _window.HideOverlay();
        _timer.Tick += (_, _) => Render(force: false);
        _settings.Changed += OnSettingsChanged;
        _monitor = PickMonitor(OverlayNative.GetForegroundWindow());
        ForegroundHook.Install(OnForeground);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>A new model from the service (UI thread): replaces the shown lines, handles the signal (Decision 8).</summary>
    public void OnRemainingTime(RemainingTimeDto model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _clock.Update(model);
        if (PillAnimation.StartsQuickPulse(model.Signal))
        {
            _quickPulseStarted = _time.GetTimestamp();
        }

        if (PillAnimation.PlaysSound(model.Signal, _settings.Current.Sound))
        {
            SystemSounds.Exclamation.Play();
        }

        Render(force: false);
    }

    /// <summary>The connection to the service was lost: hide at once (AC-29).</summary>
    public void OnDisconnected()
    {
        _clock.Clear();
        Render(force: false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ForegroundHook.Remove();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _settings.Changed -= OnSettingsChanged;
        _timer.Dispose();
        _window.Dispose();
        _renderer.Dispose();
    }

    private void OnSettingsChanged() => Render(force: true);

    private void Render(bool force)
    {
        var lines = _clock.Current();
        if (lines is null)
        {
            _timer.Stop();
            Hide();
            return;
        }

        var settings = _settings.Current;
        var layout = OverlayLayout.Build(lines, settings.ShowFromMinutes, _clock.EarlyMinutes, _clock.FinalMinutes, CultureInfo.CurrentUICulture);
        var pulse = PillAnimation.Compute(settings.OpacityPercent, SinceQuickPulse(), layout.AnyFinalAboveZero, _time.GetElapsedTime(_started));
        ScheduleNextTick(pulse.Pulsing);
        if (layout.Lines.Count == 0)
        {
            Hide();
            return;
        }

        Present(layout, pulse.BackgroundAlpha, settings.Corner, force);
        if (_time.GetElapsedTime(_lastUpkeep) >= UpkeepInterval)
        {
            _lastUpkeep = _time.GetTimestamp();
            _window.Reassert();
        }
    }

    private void Present(PillLayout layout, byte alpha, OverlayCorner corner, bool force)
    {
        var dpi = OverlayNative.MonitorDpi(_monitor);
        var bounds = OverlayNative.MonitorBounds(_monitor);
        var key = string.Join("\n", layout.Lines.Select(l => $"{l.Text}|{l.Level}")) + $"|{alpha}|{dpi}|{corner}|{bounds}";
        if (force || key != _lastKey || !_visible)
        {
            var bottom = corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight;
            var bitmap = _renderer.Render(layout, alpha, dpi, bottom);
            var margin = MarginLogicalPx * dpi / OverlayNative.DefaultDpi;
            var x = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft ? bounds.Left + margin : bounds.Right - margin - bitmap.Width;
            var y = bottom ? bounds.Bottom - margin - bitmap.Height : bounds.Top + margin;
            _window.Present(bitmap, new Point(x, y));
            _lastKey = key;
        }

        if (!_visible)
        {
            _window.ShowNoActivate();
            _window.Reassert();
            _visible = true;
        }
    }

    private void Hide()
    {
        if (_visible)
        {
            _window.HideOverlay();
            _visible = false;
        }
    }

    private void ScheduleNextTick(bool pulsing)
    {
        _timer.Interval = pulsing ? PulseIntervalMs : (int)Math.Min(IdleIntervalMs, _clock.UntilNextSecond().TotalMilliseconds);
        _timer.Start();
    }

    private TimeSpan? SinceQuickPulse() => _quickPulseStarted is { } started ? _time.GetElapsedTime(started) : null;

    private void OnForeground(nint hwnd)
    {
        var monitor = PickMonitor(hwnd);
        if (monitor != _monitor)
        {
            _monitor = monitor;
            Render(force: true);
        }

        if (_visible)
        {
            _window.Reassert();
        }
    }

    /// <summary>The monitor of the foreground window; the tray's own windows and the shell keep the current one (spike S-7).</summary>
    private nint PickMonitor(nint foreground)
    {
        if (foreground != 0 && OverlayNative.ProcessIdOf(foreground) != _ownPid && !IgnoredForegroundClasses.Contains(OverlayNative.ClassName(foreground)))
        {
            return OverlayNative.MonitorFromWindow(foreground, OverlayNative.MONITOR_DEFAULTTONEAREST);
        }

        return _monitor != 0 ? _monitor : OverlayNative.MonitorFromPoint(default, OverlayNative.MONITOR_DEFAULTTOPRIMARY);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                // AC-25: no pill while locked; the next push after the unlock brings the correct time.
                _clock.Clear();
                Render(force: false);
                break;
            case SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                _monitor = PickMonitor(OverlayNative.GetForegroundWindow());
                Render(force: true);
                break;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _monitor = 0;
        _monitor = PickMonitor(OverlayNative.GetForegroundWindow());
        Render(force: true);
    }
}
