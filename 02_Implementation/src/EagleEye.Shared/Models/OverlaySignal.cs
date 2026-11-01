namespace EagleEye.Shared.Models;

/// <summary>
/// The strongest level reached in one engine tick, decided by the service's warning ledger (US-010 AC-8, ADR-022 §6).
/// The tray pulses and plays the sound once per push with a signal other than <see cref="None"/>.
/// </summary>
public enum OverlaySignal
{
    /// <summary>No new level in this tick.</summary>
    None,

    /// <summary>The early level was reached: quick pulse and sound.</summary>
    Early,

    /// <summary>The final level was reached or a grace period started: sound (the slow pulse follows the state).</summary>
    Final,

    /// <summary>An end was reached and the apps are being closed: sound only.</summary>
    Expired,
}
