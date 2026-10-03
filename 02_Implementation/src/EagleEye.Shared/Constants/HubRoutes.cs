namespace EagleEye.Shared.Constants;

/// <summary>
/// SignalR hub route paths. Used by the server (hub mapping) and by clients (connection URL).
/// </summary>
public static class HubRoutes
{
    /// <summary>Route of the hub that tray clients connect to.</summary>
    public const string Tray = "/hubs/tray";
}
