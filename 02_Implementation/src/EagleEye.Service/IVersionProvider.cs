using EagleEye.Shared.Models;

namespace EagleEye.Service;

/// <summary>
/// Provides the version identifier of the running service.
/// </summary>
public interface IVersionProvider
{
    /// <summary>
    /// Returns the service version in the format "EagleEye_vMAJOR.MINOR".
    /// </summary>
    ServiceVersionDto GetVersion();
}
