namespace EagleEye.Shared.Models;

/// <summary>The warning level of an overlay line; decides its colour (US-010 AC-7).</summary>
public enum OverlayLevel
{
    /// <summary>More than the early level left: white.</summary>
    Normal,

    /// <summary>At or below the early level: yellow.</summary>
    Early,

    /// <summary>At or below the final level: red.</summary>
    Final,

    /// <summary>0:00: steady red until the service has closed the apps.</summary>
    Expired,
}
