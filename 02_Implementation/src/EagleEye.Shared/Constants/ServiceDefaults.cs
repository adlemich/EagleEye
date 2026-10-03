namespace EagleEye.Shared.Constants;

/// <summary>
/// Default configuration values shared across components.
/// </summary>
public static class ServiceDefaults
{
    /// <summary>Default HTTP port of the service (used until TLS is introduced).</summary>
    public const int ServicePort = 5080;

    /// <summary>Default service base URL for localhost connections.</summary>
    public const string LocalBaseUrl = "http://localhost:5080";
}
