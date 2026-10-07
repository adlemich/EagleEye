using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Reads process facts with <c>OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)</c>, <c>QueryFullProcessImageName</c>,
/// <c>ProcessIdToSessionId</c>, the token user, <c>GetProcessTimes</c> and <c>GetPackageFullName</c>. Never reads
/// the process's memory or files. Thin Win32, verified manually.
/// </summary>
public sealed unsafe partial class Win32ProcessInspector : IProcessInspector
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenUser = 1;
    private const int MaxPath = 32_767;

    /// <inheritdoc />
    public long? GetCreationTime(int processId)
    {
        using var process = Open(processId);
        return process is null ? null : CreationTime(process);
    }

    /// <inheritdoc />
    public ProcessFacts? Inspect(int processId)
    {
        using var process = Open(processId);
        if (process is null || CreationTime(process) is not { } created || ImagePath(process) is not { } path
            || !ProcessIdToSessionId((uint)processId, out var sessionId))
        {
            return null;
        }

        return new ProcessFacts(processId, (int)sessionId, OwnerSid(process), created, path, PackageFullName(process));
    }

    private static SafeProcessHandle? Open(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return handle;
    }

    private static long? CreationTime(SafeProcessHandle process)
    {
        return GetProcessTimes(process, out var created, out _, out _, out _) ? created : null;
    }

    private static string? ImagePath(SafeProcessHandle process)
    {
        var buffer = new char[MaxPath];
        var size = (uint)buffer.Length;
        fixed (char* data = buffer)
        {
            return QueryFullProcessImageNameW(process, 0, data, ref size) ? new string(data, 0, (int)size) : null;
        }
    }

    private static string? OwnerSid(SafeProcessHandle process)
    {
        if (!OpenProcessToken(process, TokenQuery, out var token))
        {
            return null;
        }

        using (token)
        {
            _ = GetTokenInformation(token, TokenUser, null, 0, out var needed);
            var buffer = new byte[needed];
            fixed (byte* data = buffer)
            {
                return needed > 0 && GetTokenInformation(token, TokenUser, data, needed, out _)
                    ? new SecurityIdentifier(*(nint*)data).Value
                    : null;
            }
        }
    }

    private static string? PackageFullName(SafeProcessHandle process)
    {
        var length = 0u;
        if (GetPackageFullName(process, ref length, null) != 122 || length == 0) // ERROR_INSUFFICIENT_BUFFER: packaged
        {
            return null;
        }

        var buffer = new char[length];
        fixed (char* data = buffer)
        {
            return GetPackageFullName(process, ref length, data) == 0 ? new string(data, 0, (int)length - 1) : null;
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [LibraryImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [LibraryImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, byte* information, uint length, out uint returnLength);

    [LibraryImport("kernel32.dll")]
    private static partial int GetPackageFullName(SafeProcessHandle process, ref uint length, char* name);
}
