using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.UserAccounts;

/// <summary>
/// Broadcasts <see cref="IParentClientCallback.OnUserAccountsChanged"/> to the group
/// <see cref="ParentHub.ParentsGroup"/> (paired connections only, ADR-010 §8).
/// </summary>
public sealed class UserAccountsBroadcaster(IHubContext<ParentHub, IParentClientCallback> hub) : IUserAccountsBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAsync(UserAccountListDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnUserAccountsChanged(snapshot);
    }
}
