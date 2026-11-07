using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.Overlay;

/// <summary>A line as the tray shows it now: its kind, name and displayed seconds.</summary>
internal sealed record DisplayedLine(OverlayLineKind Kind, string? Name, int Seconds);

/// <summary>
/// The last remaining-time model of the service, counted down locally between pushes (US-010 Decision 7, ADR-022 §6):
/// counting lines lose one second per whole second since the push (never below 0), the others stay. A model older than
/// <see cref="StaleAfter"/> is dropped, so the tray never shows a time the service has not confirmed (AC-29, OQ-11). Pure.
/// </summary>
internal sealed class RemainingTimeClock(TimeProvider time)
{
    /// <summary>No push for this long → the model is dropped and the overlay hides.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(15);

    private const double MillisecondsPerSecond = 1000;
    private const double TickSlackMilliseconds = 20;
    private const double MinTickMilliseconds = 15;

    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private RemainingTimeDto? _model;
    private long _received;

    /// <summary>The early-warning minutes of the current model (the defaults when there is none).</summary>
    public int EarlyMinutes => _model?.EarlyMinutes ?? Shared.Constants.WarningRules.DefaultEarlyMinutes;

    /// <summary>The final-warning minutes of the current model (the defaults when there is none).</summary>
    public int FinalMinutes => _model?.FinalMinutes ?? Shared.Constants.WarningRules.DefaultFinalMinutes;

    /// <summary>Whether a model is held (it may still turn out stale on the next <see cref="Current"/>).</summary>
    public bool HasModel => _model is not null;

    /// <summary>Replaces the model and takes the receive time.</summary>
    public void Update(RemainingTimeDto model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _received = _time.GetTimestamp();
    }

    /// <summary>Drops the model (connection lost, session locked).</summary>
    public void Clear() => _model = null;

    /// <summary>The lines with their displayed seconds now, or <c>null</c> when there is no model or it is stale (then dropped).</summary>
    public IReadOnlyList<DisplayedLine>? Current()
    {
        if (_model is null)
        {
            return null;
        }

        var elapsed = _time.GetElapsedTime(_received);
        if (elapsed >= StaleAfter)
        {
            _model = null;
            return null;
        }

        var whole = (int)elapsed.TotalSeconds;
        return [.. _model.Lines.Select(l => new DisplayedLine(l.Kind, l.Name, l.Counting ? Math.Max(0, l.Seconds - whole) : l.Seconds))];
    }

    /// <summary>Time until the displayed seconds change next (the next whole second after the push, plus 20 ms).</summary>
    public TimeSpan UntilNextSecond()
    {
        var elapsed = _time.GetElapsedTime(_received).TotalMilliseconds;
        var next = MillisecondsPerSecond - (elapsed % MillisecondsPerSecond) + TickSlackMilliseconds;
        return TimeSpan.FromMilliseconds(Math.Clamp(next, MinTickMilliseconds, MillisecondsPerSecond));
    }
}
