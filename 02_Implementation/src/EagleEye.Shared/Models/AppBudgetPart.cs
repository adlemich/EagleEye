namespace EagleEye.Shared.Models;

/// <summary>The part of an app's daily budget that a field-level write changes (US-006 AC-8, AC-17).</summary>
public enum AppBudgetPart
{
    /// <summary>The hours (0 … 23).</summary>
    Hours,

    /// <summary>The minutes (0 … 59).</summary>
    Minutes,
}
