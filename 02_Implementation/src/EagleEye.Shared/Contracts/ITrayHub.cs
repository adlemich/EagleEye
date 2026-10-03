using EagleEye.Shared.Models;

namespace EagleEye.Shared.Contracts;

/// <summary>
/// Server-side hub interface for tray client connections.
/// The tray client invokes these methods on the service.
/// </summary>
public interface ITrayHub
{
    /// <summary>
    /// Returns the service version identifier (e.g., "EagleEye_v0.1").
    /// </summary>
    Task<ServiceVersionDto> GetServiceVersion();
}
