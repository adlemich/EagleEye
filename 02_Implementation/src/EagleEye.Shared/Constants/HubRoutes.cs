namespace EagleEye.Shared.Constants;

/// <summary>
/// SignalR hub route paths. Used by the server (hub mapping) and by clients (connection URL).
/// </summary>
public static class HubRoutes
{
    /// <summary>Route of the hub that tray clients connect to (loopback HTTP endpoint only).</summary>
    public const string Tray = "/hubs/tray";

    /// <summary>Route of the hub that parent apps connect to (HTTPS endpoint only, ADR-008).</summary>
    public const string Parent = "/hubs/parent";
}
