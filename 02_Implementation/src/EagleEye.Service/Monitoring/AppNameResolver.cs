namespace EagleEye.Service.Monitoring;

/// <summary>
/// The display name of an app (US-004 AC-8, ADR-012 §1): Store package name, then the file description, then the
/// process name without <c>.exe</c>. Every name is sanitized (<see cref="DisplayNameSanitizer"/>). Cached per program
/// path and package; a failing metadata source gives the process name and a warning. Thread-safe.
/// </summary>
public sealed class AppNameResolver(IAppMetadataSource metadata, ILogger<AppNameResolver> logger)
{
    private readonly Dictionary<(string Path, string? Package), string> _cache = new(CacheKeyComparer.Instance);
    private readonly Lock _lock = new();

    /// <summary>Returns the display name of the process's program.</summary>
    public string Resolve(ProcessFacts process)
    {
        ArgumentNullException.ThrowIfNull(process);
        var key = (process.ImagePath, process.PackageFullName);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var name = ResolveUncached(process);
        lock (_lock)
        {
            _cache[key] = name;
        }

        return name;
    }

    /// <summary>The process name without <c>.exe</c> (FR-APP-032), e.g. <c>mygame</c> for <c>mygame.exe</c>.</summary>
    public static string ProcessNameWithoutExtension(string imagePath)
    {
        ArgumentNullException.ThrowIfNull(imagePath);
        var fileName = Path.GetFileName(imagePath);
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? fileName[..^4] : fileName;
    }

    private string ResolveUncached(ProcessFacts process)
    {
        var fallback = DisplayNameSanitizer.Sanitize(ProcessNameWithoutExtension(process.ImagePath)) ?? "?";
        AppMetadata found;
        try
        {
            found = metadata.Read(process);
        }
        catch (Exception ex)
        {
            // Name boundary (coding guidelines §12.4): any failure falls back to the process name.
            logger.LogWarning(ex, "Reading the name of {ProgramPath} failed; the process name is used.", process.ImagePath);
            return fallback;
        }

        return DisplayNameSanitizer.Sanitize(found.PackageDisplayName)
            ?? DisplayNameSanitizer.Sanitize(found.FileDescription)
            ?? fallback;
    }

    private sealed class CacheKeyComparer : IEqualityComparer<(string Path, string? Package)>
    {
        public static readonly CacheKeyComparer Instance = new();

        public bool Equals((string Path, string? Package) x, (string Path, string? Package) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path) && StringComparer.OrdinalIgnoreCase.Equals(x.Package, y.Package);

        public int GetHashCode((string Path, string? Package) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path), obj.Package?.ToUpperInvariant());
    }
}
