using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Rules;

/// <summary>
/// Broadcasts the rules state areas (<see cref="IParentClientCallback.OnAccountRulesChanged"/>, <see cref="IParentClientCallback.OnAppRulesChanged"/>,
/// <see cref="IParentClientCallback.OnWarningSettingsChanged"/>, and the app-group areas of US-008) to the group <see cref="ParentHub.ParentsGroup"/>
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

    /// <inheritdoc />
    public Task BroadcastAsync(AppRulesDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnAppRulesChanged(snapshot);
    }

    /// <inheritdoc />
    public Task BroadcastAsync(WarningSettingsDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnWarningSettingsChanged(snapshot);
    }

    /// <inheritdoc />
    public Task BroadcastAsync(AppGroupsDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnAppGroupsChanged(snapshot);
    }

    /// <inheritdoc />
    public Task BroadcastAsync(AppGroupRulesDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnAppGroupRulesChanged(snapshot);
    }
}
