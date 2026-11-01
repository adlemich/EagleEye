using EagleEye.Shared.Models;

namespace EagleEye.TrayClient.Overlay;

/// <summary>The pill's background alpha now and whether it is pulsing (the timer then runs at 100 ms).</summary>
internal sealed record PillPulse(byte BackgroundAlpha, bool Pulsing);

/// <summary>
/// Pulse and sound of the pill (US-010 Decision 8, spike S-15, S-16): a signal plays the sound once when the sound is on;
/// the early signal starts a quick pulse (3 s at 4 Hz); a slow pulse (1 Hz) runs while a shown line is red and above 0:00.
/// The background alpha swings between a quarter of the chosen opacity and max(opacity, 230). Pure.
/// </summary>
internal static class PillAnimation
{
    /// <summary>How long the quick pulse of the early level runs.</summary>
    public static readonly TimeSpan QuickPulseDuration = TimeSpan.FromSeconds(3);

    private const double QuickHz = 4;
    private const double SlowHz = 1;
    private const double MaxAlpha = 255;
    private const double PulseHighAlpha = 230;
    private const double PulseLowFactor = 0.25;
    private const double PercentBase = 100;

    /// <summary>The background alpha now.</summary>
    /// <param name="opacityPercent">The chosen opacity.</param>
    /// <param name="sinceQuickPulse">Time since the last early signal, or <c>null</c>.</param>
    /// <param name="slowPulse">Whether a shown line is red and above 0:00.</param>
    /// <param name="clock">A monotonic clock for the pulse phase.</param>
    public static PillPulse Compute(int opacityPercent, TimeSpan? sinceQuickPulse, bool slowPulse, TimeSpan clock)
    {
        var baseAlpha = opacityPercent * MaxAlpha / PercentBase;
        var hz = sinceQuickPulse is { } since && since < QuickPulseDuration ? QuickHz : slowPulse ? SlowHz : 0;
        if (hz == 0)
        {
            return new PillPulse(ToByte(baseAlpha), false);
        }

        var phase = (Math.Sin(2 * Math.PI * hz * clock.TotalSeconds) + 1) / 2;
        var low = baseAlpha * PulseLowFactor;
        var high = Math.Max(baseAlpha, PulseHighAlpha);
        return new PillPulse(ToByte(low + ((high - low) * phase)), true);
    }

    /// <summary>Whether a push with <paramref name="signal"/> plays the sound (once per push, AC-11, AC-16).</summary>
    public static bool PlaysSound(OverlaySignal signal, bool soundOn) => soundOn && signal != OverlaySignal.None;

    /// <summary>Whether a push with <paramref name="signal"/> starts the quick pulse (the early level).</summary>
    public static bool StartsQuickPulse(OverlaySignal signal) => signal == OverlaySignal.Early;

    private static byte ToByte(double alpha) => (byte)Math.Clamp(Math.Round(alpha), 0, MaxAlpha);
}
