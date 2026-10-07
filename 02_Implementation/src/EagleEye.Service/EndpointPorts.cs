using EagleEye.Shared.Constants;

namespace EagleEye.Service;

/// <summary>The ports of the two endpoints (ADR-008 §1): tray clients on loopback HTTP, parent apps on TLS.</summary>
/// <param name="TrayPort">The loopback HTTP port of <c>/hubs/tray</c>.</param>
/// <param name="ParentPort">The TLS port of <c>/hubs/parent</c>.</param>
public sealed record EndpointPorts(int TrayPort, int ParentPort)
{
    /// <summary>The standard ports 5080 and 5443; in Debug builds shifted by <c>EAGLEEYE_DEV_PORT_OFFSET</c>.</summary>
    public static EndpointPorts Resolve()
    {
#if DEBUG
        var offset = DevSettings.PortOffset();
#else
        const int offset = 0;
#endif
        return new EndpointPorts(ServiceDefaults.ServicePort + offset, ServiceDefaults.ParentPort + offset);
    }
}
