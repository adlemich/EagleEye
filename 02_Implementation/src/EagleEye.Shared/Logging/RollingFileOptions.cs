namespace EagleEye.Shared.Logging;

/// <summary>
/// Settings of the EagleEye rolling log files (ADR-002 implementation note, FR-SVC-103): file
/// name <c>{FilePrefix}-NNN.log</c> in <see cref="Directory"/>, at most <see cref="MaxFileBytes"/>
/// per file, at most <see cref="MaxFiles"/> files, and no file older than <see cref="MaxAge"/>
/// except the current one.
/// </summary>
public sealed class RollingFileOptions
{
    /// <summary>Default size limit of one log file (50 MB).</summary>
    public const long DefaultMaxFileBytes = 50L * 1024 * 1024;

    /// <summary>Default number of log files kept.</summary>
    public const int DefaultMaxFiles = 3;

    /// <summary>Default maximum age of a non-current log file.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(5);

    /// <summary>The folder of the log files. It is never created by the writer (its ACL is set by the owner).</summary>
    public required string Directory { get; init; }

    /// <summary>The file name prefix, e.g. <c>EagleEye.Service</c>.</summary>
    public required string FilePrefix { get; init; }

    /// <summary>Size limit of one file in bytes.</summary>
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    /// <summary>Number of files kept, including the current one.</summary>
    public int MaxFiles { get; init; } = DefaultMaxFiles;

    /// <summary>Non-current files whose last write is older than this are deleted.</summary>
    public TimeSpan MaxAge { get; init; } = DefaultMaxAge;
}
