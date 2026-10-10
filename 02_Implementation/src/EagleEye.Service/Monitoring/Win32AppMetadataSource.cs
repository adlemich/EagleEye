using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Reads the names of a kid's program with the rules of coding guidelines §12.4 (ADR-011 §7 items 12 to 15):
/// <list type="bullet">
/// <item>Every file access runs while impersonating the session user (<c>WTSQueryUserToken</c> +
/// <see cref="WindowsIdentity.RunImpersonated"/>); as SYSTEM without that token nothing is read.</item>
/// <item>Paths are classified with <see cref="ProgramPathPolicy"/> before and after opening (final path via
/// <c>GetFinalPathNameByHandle</c>); only local fixed-drive files are opened.</item>
/// <item>Files in admin-only locations whose owner is SYSTEM, Administrators or TrustedInstaller are read with the
/// Windows version API (MUI-localized names); all other files only with <see cref="VersionResourceReader"/>.</item>
/// <item>Store packages: manifest via <see cref="PackageManifestReader"/>, only below <c>WindowsApps</c>;
/// <c>SHLoadIndirectString</c> only with a reference built by the service.</item>
/// <item>Never icons, shell items, COM or <c>LoadLibrary</c> of user files.</item>
/// </list>
/// Thin Win32, verified manually.
/// </summary>
public sealed unsafe partial class Win32AppMetadataSource(ProgramPathPolicy policy) : IAppMetadataSource
{
    private const uint GenericRead = 0x80000000;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint FileNameNormalized = 0x0;
    private const int SeFileObject = 1;
    private const uint OwnerSecurityInformation = 0x1;
    private const uint FileVerGetLocalisedAndNeutral = 0x3;
    private const string ManifestName = "AppxManifest.xml";

    private static readonly bool IsSystem = WindowsIdentity.GetCurrent().IsSystem;

    /// <inheritdoc />
    public AppMetadata Read(ProcessFacts process)
    {
        ArgumentNullException.ThrowIfNull(process);
        using var token = UserToken(process.SessionId);
        if (token is null)
        {
            // Console mode (DEV, Debug): the service already runs as the user. As SYSTEM: never read without impersonation.
            return IsSystem ? AppMetadata.None : ReadCore(process);
        }

        return WindowsIdentity.RunImpersonated(token, () => ReadCore(process));
    }

    private AppMetadata ReadCore(ProcessFacts process)
    {
        var package = process.PackageFullName is { } fullName ? PackageDisplayName(fullName, process.ImagePath) : null;
        return new AppMetadata(package, FileDescription(process.ImagePath));
    }

    private string? FileDescription(string path)
    {
        using var file = OpenLocal(path);
        if (file is null || FinalLocalPath(file) is not { } final)
        {
            return null;
        }

        if (policy.Classify(final) == PathClass.LocalTrusted && ProgramPathPolicy.IsTrustedOwner(Owner(file)))
        {
            return WindowsFileDescription(policy.NormalizeLocal(final)!); // Classified local, so not null.
        }

        using var stream = new FileStream(file, FileAccess.Read, 4096, isAsync: false);
        return VersionResourceReader.ReadFileDescription(stream);
    }

    private string? PackageDisplayName(string fullName, string imagePath)
    {
        var packagePath = PackagePath(fullName);
        if (!policy.IsInWindowsApps(packagePath))
        {
            return null;
        }

        var manifestPath = Path.Combine(packagePath!, ManifestName); // IsInWindowsApps is false for null.
        using var file = OpenLocal(manifestPath);
        if (file is null || FinalLocalPath(file) is not { } final || !policy.IsInWindowsApps(final))
        {
            return null;
        }

        using var stream = new FileStream(file, FileAccess.Read, 4096, isAsync: false);
        var executable = imagePath.StartsWith(packagePath + "\\", StringComparison.OrdinalIgnoreCase) ? imagePath[(packagePath!.Length + 1)..] : null;
        var name = PackageManifestReader.ReadDisplayName(stream, executable);
        if (name is null || !name.IsResource)
        {
            return name?.Value;
        }

        return PackageManifestReader.BuildResourceReference(fullName, name.Value) is { } reference ? LoadIndirectString(reference) : null;
    }

    private SafeFileHandle? OpenLocal(string path)
    {
        if (policy.NormalizeLocal(path) is not { } local)
        {
            return null;
        }

        var handle = CreateFileW(local, GenericRead, ShareAll, 0, OpenExisting, 0, 0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return handle;
    }

    private string? FinalLocalPath(SafeFileHandle file)
    {
        var buffer = new char[ProgramPathPolicy.MaxPathLength + 1];
        fixed (char* data = buffer)
        {
            var length = GetFinalPathNameByHandleW(file, data, (uint)buffer.Length, FileNameNormalized);
            var final = length > 0 && length < buffer.Length ? new string(data, 0, (int)length) : null;
            return policy.NormalizeLocal(final) is not null ? final : null;
        }
    }

    private static string? Owner(SafeFileHandle file)
    {
        if (GetSecurityInfo(file, SeFileObject, OwnerSecurityInformation, out var owner, 0, 0, 0, out var descriptor) != 0)
        {
            return null;
        }

        try
        {
            return new SecurityIdentifier(owner).Value;
        }
        finally
        {
            _ = LocalFree(descriptor);
        }
    }

    private static string? WindowsFileDescription(string path)
    {
        var size = GetFileVersionInfoSizeExW(FileVerGetLocalisedAndNeutral, path, out _);
        if (size == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        fixed (byte* data = buffer)
        {
            if (!GetFileVersionInfoExW(FileVerGetLocalisedAndNeutral, path, 0, size, data)
                || !VerQueryValueW(data, @"\VarFileInfo\Translation", out var translation, out var translationLength)
                || translationLength < 4)
            {
                return null;
            }

            var language = *(ushort*)translation;
            var codePage = *((ushort*)translation + 1);
            return VerQueryValueW(data, $@"\StringFileInfo\{language:X4}{codePage:X4}\FileDescription", out var value, out var chars) && chars > 0
                ? new string((char*)value, 0, (int)chars).TrimEnd('\0')
                : null;
        }
    }

    private static string? PackagePath(string fullName)
    {
        var length = 0u;
        _ = GetPackagePathByFullName(fullName, ref length, null);
        if (length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        fixed (char* data = buffer)
        {
            return GetPackagePathByFullName(fullName, ref length, data) == 0 ? new string(data, 0, (int)length - 1) : null;
        }
    }

    private static string? LoadIndirectString(string reference)
    {
        var buffer = stackalloc char[512];
        return SHLoadIndirectString(reference, buffer, 512, 0) == 0 ? new string(buffer) : null;
    }

    private static SafeAccessTokenHandle? UserToken(int sessionId)
    {
        if (WTSQueryUserToken((uint)sessionId, out var token))
        {
            return token;
        }

        token.Dispose();
        return null;
    }

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQueryUserToken(uint sessionId, out SafeAccessTokenHandle token);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(
        string path, uint access, uint share, nint securityAttributes, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetFinalPathNameByHandleW(SafeFileHandle file, char* path, uint length, uint flags);

    [LibraryImport("advapi32.dll")]
    private static partial uint GetSecurityInfo(
        SafeFileHandle handle, int objectType, uint securityInfo, out nint owner, nint group, nint dacl, nint sacl, out nint descriptor);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    [LibraryImport("version.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetFileVersionInfoSizeExW(uint flags, string path, out uint handle);

    [LibraryImport("version.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileVersionInfoExW(uint flags, string path, uint handle, uint length, byte* data);

    [LibraryImport("version.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VerQueryValueW(byte* block, string subBlock, out nint buffer, out uint length);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetPackagePathByFullName(string fullName, ref uint length, char* path);

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHLoadIndirectString(string source, char* output, uint outputLength, nint reserved);
}
