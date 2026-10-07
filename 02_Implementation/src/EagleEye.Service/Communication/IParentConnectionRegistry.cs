using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>Tracks the paired parent connections per device, so that they can be closed.</summary>
public interface IParentConnectionRegistry
{
    /// <summary>Records a paired connection of the device.</summary>
    void Register(string deviceId, HubCallerContext connection);

    /// <summary>Forgets a connection of the device.</summary>
    void Unregister(string deviceId, string connectionId);

    /// <summary>Aborts and forgets all connections of the device except <paramref name="exceptConnectionId"/>.</summary>
    void AbortAll(string deviceId, string? exceptConnectionId);
}
