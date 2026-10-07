using EagleEye.Service.Communication;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Statistics;

/// <summary>Broadcasts <see cref="IParentClientCallback.OnDayUsageChanged"/> to the group <see cref="ParentHub.ParentsGroup"/>.</summary>
public sealed class UsageBroadcaster(IHubContext<ParentHub, IParentClientCallback> hub) : IUsageBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastAsync(DayUsageDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return hub.Clients.Group(ParentHub.ParentsGroup).OnDayUsageChanged(snapshot);
    }
}
