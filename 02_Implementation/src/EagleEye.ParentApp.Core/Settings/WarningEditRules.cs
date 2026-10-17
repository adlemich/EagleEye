using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Constants;

namespace EagleEye.ParentApp.Core.Settings;

/// <summary>What is wrong with an edited warning level (US-006 AC-15).</summary>
public enum WarningEditProblem
{
    /// <summary>Valid.</summary>
    None,

    /// <summary>Not a whole number from 1 to 59.</summary>
    InvalidMinutes,

    /// <summary>The final warning would not be less minutes before the end than the early warning.</summary>
    FinalNotLess,
}

/// <summary>Client-side checks of the warning levels (US-006 AC-15), against the confirmed other level. Pure.</summary>
public static class WarningEditRules
{
    /// <summary>Parses a level (1 … 59): ASCII digits only, trimmed, at most two digits.</summary>
    public static bool TryParseMinutes(string? text, out int minutes) =>
        AppBudgetText.TryParse(text, WarningRules.MinMinutes, WarningRules.MaxMinutes, out minutes);

    /// <summary>Whether early is greater than final (both valid).</summary>
    public static bool IsValidPair(int earlyMinutes, int finalMinutes) => WarningRules.IsValidPair(earlyMinutes, finalMinutes);
}
