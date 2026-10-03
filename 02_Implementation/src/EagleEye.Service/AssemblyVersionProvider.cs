using System.Reflection;
using EagleEye.Shared.Models;

namespace EagleEye.Service;

/// <summary>
/// Reads the version from an assembly's <see cref="AssemblyInformationalVersionAttribute"/>
/// (set via <c>VersionPrefix</c> in <c>Directory.Build.props</c>) and formats it as
/// "EagleEye_vMAJOR.MINOR".
/// </summary>
public sealed class AssemblyVersionProvider : IVersionProvider
{
    private const string VersionPrefix = "EagleEye_v";
    private const string FallbackVersion = VersionPrefix + "0.0";
    private static readonly char[] MetadataSeparators = ['+', '-'];

    private readonly ServiceVersionDto _version;

    /// <summary>
    /// Creates a provider for the given assembly. The version is read once.
    /// </summary>
    /// <param name="assembly">The assembly whose informational version is reported.</param>
    public AssemblyVersionProvider(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        _version = new ServiceVersionDto(Format(informationalVersion));
    }

    /// <inheritdoc />
    public ServiceVersionDto GetVersion()
    {
        return _version;
    }

    /// <summary>
    /// Formats an informational version such as "0.1.0+abc123" as "EagleEye_v0.1".
    /// Returns "EagleEye_v0.0" when the value is missing or not a valid version.
    /// </summary>
    internal static string Format(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return FallbackVersion;
        }

        var versionPart = informationalVersion.Split(MetadataSeparators)[0];
        return Version.TryParse(versionPart, out var version)
            ? $"{VersionPrefix}{version.Major}.{version.Minor}"
            : FallbackVersion;
    }
}
