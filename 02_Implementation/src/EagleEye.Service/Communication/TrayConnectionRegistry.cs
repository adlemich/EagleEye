using System.Net;

namespace EagleEye.Service.Communication;

/// <summary>The verified identity of a tray connection (ADR-014 §1).</summary>
/// <param name="SessionId">The session the tray client runs in.</param>
/// <param name="OwnerSid">The tray client's user.</param>
/// <param name="ProcessId">The tray client's process.</param>
public sealed record TrayClientIdentity(int SessionId, string? OwnerSid, int ProcessId);

/// <summary>Finds the process behind a loopback tray connection and checks that it is the installed tray client (ADR-014 §1).</summary>
public interface ITrayClientIdentifier
{
    /// <summary>The identity of the genuine tray client behind the connection, or <c>null</c> (unverified).</summary>
    /// <param name="serverEndPoint">The service's end of the connection (<c>HttpContext.Connection.LocalIpAddress/Port</c>).</param>
    /// <param name="clientEndPoint">The client's end of the connection (<c>RemoteIpAddress/Port</c>).</param>
    TrayClientIdentity? Identify(IPEndPoint serverEndPoint, IPEndPoint clientEndPoint);
}

/// <summary>
/// The verified tray connections per session (ADR-014 §1). Thread-safe. Messages go to the newest verified
/// connection of a session, so a second tray instance never causes a second dialog.
/// </summary>
public sealed class TrayConnectionRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (TrayClientIdentity Identity, long Order)> _connections = new(StringComparer.Ordinal);
    private long _order;

    /// <summary>Records a verified connection.</summary>
    public void Register(string connectionId, TrayClientIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(identity);
        lock (_lock)
        {
            _connections[connectionId] = (identity, ++_order);
        }
    }

    /// <summary>Forgets a connection (no-op if unknown).</summary>
    public void Unregister(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);
        lock (_lock)
        {
            _connections.Remove(connectionId);
        }
    }

    /// <summary>The newest verified connection of the session, or <c>null</c>.</summary>
    public string? NewestOf(int sessionId)
    {
        lock (_lock)
        {
            return _connections
                .Where(c => c.Value.Identity.SessionId == sessionId)
                .OrderByDescending(c => c.Value.Order)
                .Select(c => c.Key)
                .FirstOrDefault();
        }
    }
}
