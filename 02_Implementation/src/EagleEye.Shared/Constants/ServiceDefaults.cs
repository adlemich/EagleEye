namespace EagleEye.Shared.Constants;

/// <summary>
/// Default configuration values shared across components.
/// </summary>
public static class ServiceDefaults
{
    /// <summary>HTTP port of the tray endpoint, bound to the loopback interface only (ADR-008).</summary>
    public const int ServicePort = 5080;

    /// <summary>Default service base URL for localhost connections (tray clients).</summary>
    public const string LocalBaseUrl = "http://localhost:5080";

    /// <summary>HTTPS port of the parent endpoint, reachable from the LAN (ADR-008).</summary>
    public const int ParentPort = 5443;
}
