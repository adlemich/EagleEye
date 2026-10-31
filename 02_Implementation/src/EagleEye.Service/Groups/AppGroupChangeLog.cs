using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Shared.Constants;

namespace EagleEye.Service.Groups;

/// <summary>Pure texts for the service log (English) of app-group changes (US-008 AC-14, AC-16). Group names are logged.</summary>
public static class AppGroupChangeLog
{
    /// <summary>"app group created: Games".</summary>
    public static string Created(string name) => $"app group created: {name}";

    /// <summary>"app group renamed: Games → Gaming".</summary>
    public static string Renamed(string oldName, string newName) => $"app group renamed: {oldName} → {newName}";

    /// <summary>"app group deleted: Gaming".</summary>
    public static string Deleted(string name) => $"app group deleted: {name}";

    /// <summary>"app moved: Minecraft Launcher → Games" or "app moved: Paint → not in any group".</summary>
    public static string Moved(string displayName, string? groupName) => $"app moved: {displayName} → {groupName ?? "not in any group"}";

    /// <summary>"group rule changed: Games, 1:00, Sa Su" or "… 0:00, no day".</summary>
    public static string RuleChanged(string groupName, LimitRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return $"group rule changed: {groupName}, {AppRuleRules.Format(rule.BudgetMinutes)}, {AppRuleChangeLog.Days(rule.Days)}";
    }
}
