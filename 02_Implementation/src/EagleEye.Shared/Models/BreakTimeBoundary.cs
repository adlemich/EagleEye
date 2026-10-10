namespace EagleEye.Shared.Models;

/// <summary>Which boundary of a break-time entry a time write changes.</summary>
public enum BreakTimeBoundary
{
    /// <summary>The start time (inclusive).</summary>
    Start,

    /// <summary>The end time (exclusive; 23:59 means midnight).</summary>
    End,
}
