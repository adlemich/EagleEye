using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// <c>deviceId → connections</c> for paired parent connections. Used to close the other
/// connections of a device when it is de-registered (FR-SVC-097).
/// </summary>
public sealed class ParentConnectionRegistry : IParentConnectionRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Dictionary<string, HubCallerContext>> _connections = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(string deviceId, HubCallerContext connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(connection);

        lock (_lock)
        {
            if (!_connections.TryGetValue(deviceId, out var byConnection))
            {
                byConnection = new Dictionary<string, HubCallerContext>(StringComparer.Ordinal);
                _connections[deviceId] = byConnection;
            }

            byConnection[connection.ConnectionId] = connection;
        }
    }

    /// <inheritdoc />
    public void Unregister(string deviceId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_lock)
        {
            if (_connections.TryGetValue(deviceId, out var byConnection)
                && byConnection.Remove(connectionId)
                && byConnection.Count == 0)
            {
                _connections.Remove(deviceId);
            }
        }
    }

    /// <inheritdoc />
    public void AbortAll(string deviceId, string? exceptConnectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        List<HubCallerContext> toAbort;
        lock (_lock)
        {
            if (!_connections.TryGetValue(deviceId, out var byConnection))
            {
                return;
            }

            toAbort = byConnection.Values.Where(c => c.ConnectionId != exceptConnectionId).ToList();
            foreach (var connection in toAbort)
            {
                byConnection.Remove(connection.ConnectionId);
            }

            if (byConnection.Count == 0)
            {
                _connections.Remove(deviceId);
            }
        }

        foreach (var connection in toAbort)
        {
            connection.Abort();
        }
    }

    /// <summary>Number of registered connections of the device (for tests and diagnostics).</summary>
    internal int CountFor(string deviceId)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(deviceId, out var byConnection) ? byConnection.Count : 0;
        }
    }
}
