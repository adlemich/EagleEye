namespace EagleEye.Service.Diagnostics;

/// <summary>
/// Applies <see cref="LogDirectorySecurity"/> to the service log folder on every start, so that a
/// folder created or altered by someone else is repaired before the service writes a log file into
/// it (US-003 plan, "Admin-only log folder"). Thin Windows wrapper, not unit-tested; verified manually.
/// </summary>
public static class LogDirectoryProtector
{
    /// <summary>Creates the folder if it is missing and sets its ACL.</summary>
    /// <exception cref="UnauthorizedAccessException">The caller may not change the ACL.</exception>
    /// <exception cref="IOException">The folder cannot be created.</exception>
    public static void Protect(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var folder = Directory.CreateDirectory(directory);
        folder.SetAccessControl(LogDirectorySecurity.Create());
    }
}
