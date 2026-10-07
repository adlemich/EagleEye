namespace EagleEye.Service.Monitoring;

/// <summary>Names found for a program (both may be null).</summary>
/// <param name="PackageDisplayName">The Store package's display name (resolved), if the process is packaged.</param>
/// <param name="FileDescription">The <c>FileDescription</c> of the program's version resource.</param>
public sealed record AppMetadata(string? PackageDisplayName, string? FileDescription)
{
    /// <summary>Nothing found.</summary>
    public static readonly AppMetadata None = new(null, null);
}

/// <summary>Reads the names of a program (ADR-012 §1). Implementations must follow coding guidelines §12.4.</summary>
public interface IAppMetadataSource
{
    /// <summary>Reads package display name and file description of the process's program.</summary>
    AppMetadata Read(ProcessFacts process);
}
