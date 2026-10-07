using EagleEye.Shared.Constants;

namespace EagleEye.Service.Communication;

/// <summary>
/// Binds each hub to its endpoint by the local port of the TCP connection, never by the
/// <c>Host</c> header (ADR-008 §1): <c>/hubs/tray</c> only on the loopback port 5080,
/// <c>/hubs/parent</c> only on the TLS port 5443. Anything else gets 404.
/// </summary>
public sealed class HubEndpointGuard(RequestDelegate next)
{
    /// <summary>Handles the request.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.Request.Path;
        var localPort = context.Connection.LocalPort;

        if (IsBlocked(path, HubRoutes.Tray, localPort, ServiceDefaults.ServicePort)
            || IsBlocked(path, HubRoutes.Parent, localPort, ServiceDefaults.ParentPort))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool IsBlocked(PathString path, string route, int localPort, int allowedPort)
    {
        return path.StartsWithSegments(route, StringComparison.OrdinalIgnoreCase) && localPort != allowedPort;
    }
}
