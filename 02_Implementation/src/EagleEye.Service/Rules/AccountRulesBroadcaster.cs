using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Rules;

/// <summary>
/// Broadcasts <see cref="IParentClientCallback.OnAccountRulesChanged"/> to the group <see cref="ParentHub.ParentsGroup"/>
/// (paired connections only, ADR-010 §8).
/// </summary>
public sealed class AccountRulesBroadcaster(IHubContext<ParentHub, IParentClientCallback> hub) : IAccountRulesBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAsync(AccountRulesDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnAccountRulesChanged(snapshot);
    }
}
