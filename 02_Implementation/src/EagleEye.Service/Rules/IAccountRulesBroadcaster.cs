using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>Sends snapshots of the state area "AccountRules:{sid}" to all paired parent apps (ADR-010 §9).</summary>
public interface IAccountRulesBroadcaster
{
    /// <summary>Broadcasts the snapshot to the group <c>Parents</c>, including the sender of a write.</summary>
    Task BroadcastAsync(AccountRulesDto snapshot);

    /// <summary>Broadcasts a snapshot of the state area "AppRules:{sid}" (US-006) to the group <c>Parents</c>.</summary>
    Task BroadcastAsync(AppRulesDto snapshot);

    /// <summary>Broadcasts a snapshot of the global state area "WarningSettings" (US-006) to the group <c>Parents</c>.</summary>
    Task BroadcastAsync(WarningSettingsDto snapshot);
}
