using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Starts the session agent as SYSTEM inside a user session with the hardening of ADR-011 §7 items 1 to 6:
/// <list type="number">
/// <item>Token: duplicate of the service's SYSTEM token → <c>CreateRestrictedToken</c> with
/// <c>DISABLE_MAX_PRIVILEGE</c> (only <c>SeChangeNotifyPrivilege</c> stays), Administrators deny-only and
/// <c>WRITE_RESTRICTED</c> with S-1-5-12 (fallback without it, see <see cref="WriteRestrictionPolicy"/>);
/// <c>TokenSessionId</c> = target session; System integrity unchanged.</item>
/// <item>Process and thread security descriptors from <see cref="AgentStartSpec"/> (kid: no access).</item>
/// <item>Absolute application name, fixed command line, current directory = installation folder, explicit minimal
/// environment block.</item>
/// <item><c>STARTUPINFOEX</c> with <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> = exactly the agent's three pipe ends.</item>
/// <item><c>CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT</c>, desktop <c>WinSta0\Default</c>.</item>
/// </list>
/// Requires SYSTEM (SeTcbPrivilege for the session ID). Thin Win32, verified manually by Michael (DEV has no admin rights).
/// </summary>
public sealed unsafe partial class SessionAgentLauncher(AgentStartSpec spec, ILogger<SessionAgentLauncher> logger) : IAgentLauncher
{
    private const uint TokenAllAccess = 0xF01FF;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint DisableMaxPrivilege = 0x1;
    private const uint WriteRestricted = 0x8;
    private const int TokenSessionId = 12;
    private const uint HandleFlagInherit = 0x1;
    private const nint ProcThreadAttributeHandleList = 0x20002;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x400;
    private const uint ExtendedStartupInfoPresent = 0x80000;
    private const uint StartfUseStdHandles = 0x100;
    private const uint SddlRevision1 = 1;
    private const string Desktop = "WinSta0\\Default";

    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Restricted = new(WellKnownSidType.RestrictedCodeSid, null);

    private readonly WriteRestrictionPolicy _policy = new();
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public IAgentProcess Launch(int sessionId)
    {
        bool writeRestricted;
        lock (_lock)
        {
            writeRestricted = _policy.UseWriteRestriction;
        }

        using var token = CreateAgentToken(sessionId, writeRestricted);
        var stdin = CreatePipe(childReads: true);
        var stdout = CreatePipe(childReads: false);
        var stderr = CreatePipe(childReads: false);
        try
        {
            var (processId, process) = Start(token, stdin.Child, stdout.Child, stderr.Child);
            return new PipeAgentProcess(processId, process, stdin.Parent, stdout.Parent, stderr.Parent);
        }
        catch
        {
            stdin.Parent.Dispose();
            stdout.Parent.Dispose();
            stderr.Parent.Dispose();
            throw;
        }
        finally
        {
            stdin.Child.Dispose();
            stdout.Child.Dispose();
            stderr.Child.Dispose();
        }
    }

    /// <inheritdoc />
    public void ReportEarlyExit()
    {
        lock (_lock)
        {
            if (_policy.ReportEarlyExit())
            {
                logger.LogWarning(
                    "Session agents exit at start with a write-restricted token; they run without the write restriction from now on (ADR-011 §7, Q-9).");
            }
        }
    }

    /// <inheritdoc />
    public void ReportWorking()
    {
        lock (_lock)
        {
            _policy.ReportWorking();
        }
    }

    private static SafeAccessTokenHandle CreateAgentToken(int sessionId, bool writeRestricted)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAllAccess, out var own))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        using (own)
        {
            if (!DuplicateTokenEx(own, TokenAllAccess, 0, SecurityImpersonation, TokenPrimary, out var primary))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            using (primary)
            {
                var restricted = Restrict(primary, writeRestricted);
                if (!SetTokenInformation(restricted, TokenSessionId, &sessionId, sizeof(int)))
                {
                    var error = Marshal.GetLastPInvokeError();
                    restricted.Dispose();
                    throw new Win32Exception(error);
                }

                return restricted;
            }
        }
    }

    private static SafeAccessTokenHandle Restrict(SafeAccessTokenHandle primary, bool writeRestricted)
    {
        var admins = new byte[Administrators.BinaryLength];
        Administrators.GetBinaryForm(admins, 0);
        var restrictedSid = new byte[Restricted.BinaryLength];
        Restricted.GetBinaryForm(restrictedSid, 0);
        fixed (byte* adminsPtr = admins)
        fixed (byte* restrictedPtr = restrictedSid)
        {
            var disable = new SidAndAttributes { Sid = (nint)adminsPtr };
            var restrict = new SidAndAttributes { Sid = (nint)restrictedPtr };
            var ok = writeRestricted
                ? CreateRestrictedToken(primary, DisableMaxPrivilege | WriteRestricted, 1, &disable, 0, 0, 1, &restrict, out var token)
                : CreateRestrictedToken(primary, DisableMaxPrivilege, 1, &disable, 0, 0, 0, null, out token);
            return ok ? token : throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static (SafeFileHandle Parent, SafeFileHandle Child) CreatePipe(bool childReads)
    {
        var attributes = new SecurityAttributes { Length = sizeof(SecurityAttributes), InheritHandle = 1 };
        if (!CreatePipe(out var read, out var write, ref attributes, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var (parent, child) = childReads ? (write, read) : (read, write);
        _ = SetHandleInformation(parent, HandleFlagInherit, 0);
        return (parent, child);
    }

    private (int ProcessId, SafeProcessHandle Process) Start(
        SafeAccessTokenHandle token, SafeFileHandle stdin, SafeFileHandle stdout, SafeFileHandle stderr)
    {
        nint processSd = SecurityDescriptor(spec.ProcessSecurityDescriptor);
        nint threadSd = SecurityDescriptor(spec.ThreadSecurityDescriptor);
        var handles = stackalloc nint[] { stdin.DangerousGetHandle(), stdout.DangerousGetHandle(), stderr.DangerousGetHandle() };
        nuint size = 0;
        _ = InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attributeList = Marshal.AllocHGlobal((nint)size);
        var initialized = false;
        try
        {
            initialized = InitializeProcThreadAttributeList(attributeList, 1, 0, ref size);
            if (!initialized
                || !UpdateProcThreadAttribute(attributeList, 0, ProcThreadAttributeHandleList, handles, (nuint)(3 * sizeof(nint)), 0, 0))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return CreateProcess(token, stdin, stdout, stderr, attributeList, processSd, threadSd);
        }
        finally
        {
            if (initialized)
            {
                DeleteProcThreadAttributeList(attributeList);
            }

            Marshal.FreeHGlobal(attributeList);
            _ = LocalFree(processSd);
            _ = LocalFree(threadSd);
        }
    }

    private (int ProcessId, SafeProcessHandle Process) CreateProcess(
        SafeAccessTokenHandle token, SafeFileHandle stdin, SafeFileHandle stdout, SafeFileHandle stderr,
        nint attributeList, nint processSd, nint threadSd)
    {
        var processAttributes = new SecurityAttributes { Length = sizeof(SecurityAttributes), SecurityDescriptor = processSd };
        var threadAttributes = new SecurityAttributes { Length = sizeof(SecurityAttributes), SecurityDescriptor = threadSd };
        var commandLine = (spec.CommandLine + "\0").ToCharArray();
        var environment = spec.EnvironmentBlock();
        fixed (char* desktop = Desktop)
        fixed (char* application = spec.ApplicationName)
        fixed (char* command = commandLine)
        fixed (char* environmentBlock = environment)
        fixed (char* directory = spec.CurrentDirectory)
        {
            var startup = new StartupInfoEx
            {
                Cb = (uint)sizeof(StartupInfoEx),
                Desktop = desktop,
                Flags = StartfUseStdHandles,
                StdInput = stdin.DangerousGetHandle(),
                StdOutput = stdout.DangerousGetHandle(),
                StdError = stderr.DangerousGetHandle(),
                AttributeList = attributeList,
            };
            if (!CreateProcessAsUserW(
                    token, application, command, &processAttributes, &threadAttributes, true,
                    CreateNoWindow | CreateUnicodeEnvironment | ExtendedStartupInfoPresent,
                    environmentBlock, directory, &startup, out var info))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _ = CloseHandle(info.Thread);
            return ((int)info.ProcessId, new SafeProcessHandle(info.Process, ownsHandle: true));
        }
    }

    private static nint SecurityDescriptor(string sddl)
    {
        return ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, SddlRevision1, out var descriptor, out _)
            ? descriptor
            : throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public nint Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public nint SecurityDescriptor;
        public int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public uint Cb;
        public char* Reserved;
        public char* Desktop;
        public char* Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XCountChars;
        public uint YCountChars;
        public uint FillAttribute;
        public uint Flags;
        public ushort ShowWindow;
        public ushort Reserved2Size;
        public nint Reserved2;
        public nint StdInput;
        public nint StdOutput;
        public nint StdError;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint access, out SafeAccessTokenHandle token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DuplicateTokenEx(
        SafeAccessTokenHandle token, uint access, nint attributes, int impersonationLevel, int tokenType, out SafeAccessTokenHandle newToken);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateRestrictedToken(
        SafeAccessTokenHandle token, uint flags, uint disableSidCount, SidAndAttributes* sidsToDisable,
        uint deletePrivilegeCount, nint privilegesToDelete, uint restrictedSidCount, SidAndAttributes* sidsToRestrict,
        out SafeAccessTokenHandle newToken);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetTokenInformation(SafeAccessTokenHandle token, int informationClass, void* information, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes attributes, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitializeProcThreadAttributeList(nint attributeList, int count, uint flags, ref nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateProcThreadAttribute(
        nint attributeList, uint flags, nint attribute, void* value, nuint size, nint previousValue, nint returnSize);

    [LibraryImport("kernel32.dll")]
    private static partial void DeleteProcThreadAttributeList(nint attributeList);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string sddl, uint revision, out nint descriptor, out uint size);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcessAsUserW(
        SafeAccessTokenHandle token, char* applicationName, char* commandLine, SecurityAttributes* processAttributes,
        SecurityAttributes* threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags,
        void* environment, char* currentDirectory, StartupInfoEx* startupInfo, out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
