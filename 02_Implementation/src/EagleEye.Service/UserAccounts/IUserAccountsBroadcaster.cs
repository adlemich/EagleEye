using EagleEye.Shared.Models;

namespace EagleEye.Service.UserAccounts;

/// <summary>Sends snapshots of the state area "UserAccounts" to all paired parent apps (ADR-010 §9).</summary>
public interface IUserAccountsBroadcaster
{
    /// <summary>Broadcasts the snapshot to the group <c>Parents</c>, including the sender of a write.</summary>
    Task BroadcastAsync(UserAccountListDto snapshot);
}
