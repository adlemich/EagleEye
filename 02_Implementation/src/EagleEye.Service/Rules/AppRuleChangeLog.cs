using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>
/// Pure texts for the service log (English, arc42 §8.13) of app-rule and warning-settings changes (US-006 AC-11, AC-16).
/// Texts written by the parent are never logged.
/// </summary>
public static class AppRuleChangeLog
{
    /// <summary>"app rule changed: Minecraft, 0:45, Mo Tu We Th Fr" or "… 0:00, no day".</summary>
    public static string RuleChanged(AppRuleEntry app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return $"app rule changed: {app.DisplayName}, {AppRuleRules.Format(app.BudgetMinutes)}, {Days(app.Days)}";
    }

    /// <summary>"budget text changed".</summary>
    public static string TextChanged() => "budget text changed";

    /// <summary>"budget text reset to the default".</summary>
    public static string TextReset() => "budget text reset to the default";

    /// <summary>"warning levels changed: early 5 min, final 1 min".</summary>
    public static string LevelsChanged(int earlyMinutes, int finalMinutes) =>
        $"warning levels changed: early {earlyMinutes} min, final {finalMinutes} min";

    /// <summary>"early warning text changed" / "final warning text reset to the default".</summary>
    public static string WarningTextChanged(WarningLevel level, bool reset) =>
        $"{(level == WarningLevel.Early ? "early" : "final")} warning text {(reset ? "reset to the default" : "changed")}";

    /// <summary>"Mo Tu We" or "no day".</summary>
    public static string Days(BreakTimeDays days) => days == BreakTimeDays.None ? "no day" : BreakTimeChangeLog.Days(days);
}
