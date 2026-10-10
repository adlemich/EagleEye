using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>Sends snapshots of the state area "AccountRules:{sid}" to all paired parent apps (ADR-010 §9).</summary>
public interface IAccountRulesBroadcaster
{
    /// <summary>Broadcasts the snapshot to the group <c>Parents</c>, including the sender of a write.</summary>
    Task BroadcastAsync(AccountRulesDto snapshot);
}
