using System.Text;
using EagleEye.Service.SessionAgent;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// The start parameters of a session agent (ADR-011 §7 items 2 to 4; T-4, T-6). Pure, so every hardening detail
/// is unit-tested: absolute application name, fixed command line, current directory = installation folder, an
/// explicit minimal environment (never inherited), and explicit process and thread security descriptors.
/// </summary>
public sealed record AgentStartSpec(
    string ApplicationName,
    string CommandLine,
    string CurrentDirectory,
    IReadOnlyList<KeyValuePair<string, string>> Environment,
    string ProcessSecurityDescriptor,
    string ThreadSecurityDescriptor)
{
    /// <summary>
    /// Process DACL: SYSTEM full access; Administrators <c>PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE |
    /// SYNCHRONIZE</c>; protected (no inherited entries); nobody else (the kid gets no access). Owner SYSTEM.
    /// </summary>
    public const string ProcessSddl = "O:SYD:P(A;;GA;;;SY)(A;;0x101001;;;BA)";

    /// <summary>Thread DACL: SYSTEM full access; Administrators <c>THREAD_QUERY_LIMITED_INFORMATION | THREAD_TERMINATE |
    /// SYNCHRONIZE</c>; protected; nobody else. Owner SYSTEM.</summary>
    public const string ThreadSddl = "O:SYD:P(A;;GA;;;SY)(A;;0x100801;;;BA)";

    /// <summary>Builds the spec from the service executable's absolute path and the Windows folders.</summary>
    public static AgentStartSpec Create(string servicePath, string systemRoot, string systemDrive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servicePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemDrive);
        if (!Path.IsPathFullyQualified(servicePath) || servicePath.Contains('"'))
        {
            throw new ArgumentException("The service path must be absolute and must not contain quotes.", nameof(servicePath));
        }

        var temp = Path.Combine(systemRoot, "Temp");
        KeyValuePair<string, string>[] environment =
        [
            new("DOTNET_EnableDiagnostics", "0"),
            new("PATH", Path.Combine(systemRoot, "System32")),
            new("SystemDrive", systemDrive),
            new("SystemRoot", systemRoot),
            new("TEMP", temp),
            new("TMP", temp),
            new("windir", systemRoot),
        ];
        return new AgentStartSpec(
            servicePath,
            $"\"{servicePath}\" {SessionAgentHost.Argument}",
            Path.GetDirectoryName(servicePath)!, // An absolute file path always has a directory.
            environment,
            ProcessSddl,
            ThreadSddl);
    }

    /// <summary>The Unicode environment block for <c>CreateProcessAsUser</c>: sorted <c>name=value</c> strings,
    /// each terminated by NUL, plus a final NUL.</summary>
    public string EnvironmentBlock()
    {
        var block = new StringBuilder();
        foreach (var (name, value) in Environment.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }
}
