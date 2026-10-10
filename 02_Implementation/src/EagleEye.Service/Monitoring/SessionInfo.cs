namespace EagleEye.Service.Monitoring;

/// <summary>The WTS connect state of a session (<c>WTS_CONNECTSTATE_CLASS</c>).</summary>
public enum SessionConnectState
{
    /// <summary>A user is logged on and the session is connected to the console or a remote client.</summary>
    Active = 0,

    /// <summary>Connected to a client, no user logged on yet.</summary>
    Connected = 1,

    /// <summary>In the process of connecting.</summary>
    ConnectQuery = 2,

    /// <summary>Shadowing another session.</summary>
    Shadow = 3,

    /// <summary>A user is logged on but the session is not connected ("Switch user").</summary>
    Disconnected = 4,

    /// <summary>Waiting for a client.</summary>
    Idle = 5,

    /// <summary>Listening for connections.</summary>
    Listen = 6,

    /// <summary>Being reset.</summary>
    Reset = 7,

    /// <summary>Down due to an error.</summary>
    Down = 8,

    /// <summary>Initializing.</summary>
    Init = 9,
}

/// <summary>Session change notifications of the Service Control Manager that matter for usage (ADR-012 §2).</summary>
public enum SessionChangeKind
{
    /// <summary>The session was connected to the console.</summary>
    ConsoleConnect,

    /// <summary>The session was disconnected from the console ("Switch user").</summary>
    ConsoleDisconnect,

    /// <summary>The session was connected to a remote client.</summary>
    RemoteConnect,

    /// <summary>The session was disconnected from a remote client.</summary>
    RemoteDisconnect,

    /// <summary>A user logged on.</summary>
    Logon,

    /// <summary>The user logged off.</summary>
    Logoff,

    /// <summary>The session was locked.</summary>
    Lock,

    /// <summary>The session was unlocked.</summary>
    Unlock,
}

/// <summary>One session of the PC.</summary>
/// <param name="SessionId">The session ID (0 = services).</param>
/// <param name="UserSid">The logged-on user, or null if none.</param>
/// <param name="State">The WTS connect state.</param>
/// <param name="IsLocked">The session is locked.</param>
public sealed record SessionInfo(int SessionId, string? UserSid, SessionConnectState State, bool IsLocked)
{
    /// <summary>The session is the one in use at the PC: active and not locked (US-004 AC-12).</summary>
    public bool IsInUse => State == SessionConnectState.Active && !IsLocked;

    /// <summary>A user is logged on (active or switched away).</summary>
    public bool IsLoggedOn => UserSid is not null && State is SessionConnectState.Active or SessionConnectState.Disconnected;
}

/// <summary>Reads all sessions of the PC.</summary>
public interface ISessionSource
{
    /// <summary>Returns all sessions with their user, state and lock flag.</summary>
    IReadOnlyList<SessionInfo> GetSessions();
}
