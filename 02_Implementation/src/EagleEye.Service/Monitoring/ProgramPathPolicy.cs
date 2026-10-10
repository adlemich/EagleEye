namespace EagleEye.Service.Monitoring;

/// <summary>How a program path may be treated (ADR-011 §7 item 13, T-2).</summary>
public enum PathClass
{
    /// <summary>Not a usable absolute file path (empty, too long, relative, traversal, streams).</summary>
    Invalid,

    /// <summary>UNC, <c>\\?\UNC\</c> or a network drive: never opened (NTLM relay).</summary>
    Remote,

    /// <summary>A device or object-manager path (<c>\\.\</c>, <c>\\?\GLOBALROOT</c>, …): never opened.</summary>
    Device,

    /// <summary>A removable, optical or unknown drive: never opened.</summary>
    Removable,

    /// <summary>A local fixed drive below <c>%SystemRoot%</c>, <c>%ProgramFiles%</c> or <c>%ProgramFiles(x86)%</c>.</summary>
    LocalTrusted,

    /// <summary>Any other local fixed-drive path (user-writable locations).</summary>
    LocalUntrusted,
}

/// <summary>The type of a drive, as far as the policy cares.</summary>
public enum DriveKind
{
    /// <summary>A local fixed drive.</summary>
    Fixed,

    /// <summary>A removable drive.</summary>
    Removable,

    /// <summary>A network drive.</summary>
    Network,

    /// <summary>Optical, RAM, unknown or missing.</summary>
    Other,
}

/// <summary>The protected folders the policy compares with.</summary>
/// <param name="SystemRoot"><c>%SystemRoot%</c>, e.g. <c>C:\Windows</c>.</param>
/// <param name="ProgramFiles"><c>%ProgramFiles%</c>.</param>
/// <param name="ProgramFilesX86"><c>%ProgramFiles(x86)%</c>.</param>
/// <param name="InstallDirectory">EagleEye's installation folder (contains <c>Service\</c> and <c>TrayClient\</c>).</param>
public sealed record ProgramPathRoots(string SystemRoot, string ProgramFiles, string ProgramFilesX86, string InstallDirectory);

/// <summary>
/// Classifies program paths before the SYSTEM service opens any file of a kid's program (coding guidelines §12.4,
/// ADR-011 §5 and §7 item 13). Pure: the drive type is injected. Special programs (EagleEye's own, Explorer,
/// ApplicationFrameHost) are recognised by their <b>full path in the protected location</b>, never by name.
/// </summary>
public sealed class ProgramPathPolicy(ProgramPathRoots roots, Func<char, DriveKind> driveKind)
{
    /// <summary>Longest path accepted (the Windows limit for extended paths).</summary>
    public const int MaxPathLength = 32_767;

    /// <summary>SID of TrustedInstaller, the owner of Windows system files.</summary>
    public const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    private const string ExtendedPrefix = @"\\?\";

    /// <summary>Classifies a path (an image path or a final path from <c>GetFinalPathNameByHandle</c>).</summary>
    public PathClass Classify(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength || path.Contains('\0'))
        {
            return PathClass.Invalid;
        }

        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return PathClass.Remote;
        }

        if (path.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            path = path[ExtendedPrefix.Length..];
            if (!IsDriveRooted(path))
            {
                return PathClass.Device;
            }
        }
        else if (path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return PathClass.Device;
        }
        else if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return PathClass.Remote;
        }

        if (!IsDriveRooted(path) || !HasPlainSegments(path))
        {
            return PathClass.Invalid;
        }

        return driveKind(char.ToUpperInvariant(path[0])) switch
        {
            DriveKind.Network => PathClass.Remote,
            DriveKind.Fixed => IsUnder(path, roots.SystemRoot) || IsUnder(path, roots.ProgramFiles) || IsUnder(path, roots.ProgramFilesX86)
                ? PathClass.LocalTrusted
                : PathClass.LocalUntrusted,
            _ => PathClass.Removable,
        };
    }

    /// <summary>The path without the <c>\\?\</c> prefix if it is a usable local path; otherwise null.</summary>
    public string? NormalizeLocal(string? path)
    {
        if (Classify(path) is not (PathClass.LocalTrusted or PathClass.LocalUntrusted))
        {
            return null;
        }

        return path!.StartsWith(ExtendedPrefix, StringComparison.Ordinal) ? path[ExtendedPrefix.Length..] : path; // Classified, so not null.
    }

    /// <summary>A program of EagleEye's own installation folder (never recorded, AC-5).</summary>
    public bool IsEagleEyeProgram(string? path) => NormalizeLocal(path) is { } local && IsUnder(local, roots.InstallDirectory);

    /// <summary>The real Windows shell, <c>%SystemRoot%\explorer.exe</c> (US-004 AC-4).</summary>
    public bool IsSystemExplorer(string? path) => IsExactly(path, Path.Combine(roots.SystemRoot, "explorer.exe"));

    /// <summary>The real Store app frame host, <c>%SystemRoot%\System32\ApplicationFrameHost.exe</c>.</summary>
    public bool IsSystemFrameHost(string? path) => IsExactly(path, Path.Combine(roots.SystemRoot, "System32", "ApplicationFrameHost.exe"));

    /// <summary>A file below <c>%ProgramFiles%\WindowsApps</c> (installed Store packages).</summary>
    public bool IsInWindowsApps(string? path) => NormalizeLocal(path) is { } local && IsUnder(local, Path.Combine(roots.ProgramFiles, "WindowsApps"));

    /// <summary>Whether a file owner is SYSTEM, Administrators or TrustedInstaller (files a kid cannot have made).</summary>
    public static bool IsTrustedOwner(string? ownerSid) =>
        ownerSid is "S-1-5-18" or "S-1-5-32-544" or TrustedInstallerSid;

    /// <summary>Maps a <see cref="DriveType"/> to the policy's drive kind.</summary>
    public static DriveKind ToDriveKind(DriveType type)
    {
        return type switch
        {
            DriveType.Fixed => DriveKind.Fixed,
            DriveType.Removable => DriveKind.Removable,
            DriveType.Network => DriveKind.Network,
            _ => DriveKind.Other,
        };
    }

    private bool IsExactly(string? path, string expected) =>
        NormalizeLocal(path) is { } local && string.Equals(local, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    private static bool IsDriveRooted(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';

    /// <summary>No <c>.</c> or <c>..</c> segments, no empty segments, no alternate data streams, no forward slashes.</summary>
    private static bool HasPlainSegments(string path)
    {
        if (path.IndexOf(':', 2) >= 0 || path.Contains('/'))
        {
            return false;
        }

        return path[3..].Split('\\').All(segment => segment.Length > 0 && segment != "." && segment != "..");
    }
}
