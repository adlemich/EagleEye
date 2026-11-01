namespace EagleEye.Shared.Models;

/// <summary>What a line of the remaining-time overlay stands for (US-010 AC-3, ADR-022).</summary>
public enum OverlayLineKind
{
    /// <summary>A running limited app that is not in an app group.</summary>
    App,

    /// <summary>An app group with at least one running member app (ADR-021).</summary>
    Group,

    /// <summary>The next break time of the account.</summary>
    Break,
}
