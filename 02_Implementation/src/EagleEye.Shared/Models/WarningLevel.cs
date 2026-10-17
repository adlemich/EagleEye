namespace EagleEye.Shared.Models;

/// <summary>One of the two global time-elapsed warnings (FR-APP-054, US-006 AC-13).</summary>
public enum WarningLevel
{
    /// <summary>The early warning (default 5 minutes before the end).</summary>
    Early,

    /// <summary>The final warning (default 1 minute before the end).</summary>
    Final,
}
