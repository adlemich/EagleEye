using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Agent side of the command channel (ADR-011 amendment, coding guidelines §12.4): strict, bounded parsing of the
/// lines the service writes to the agent's stdin. Only the close command exists; anything else is rejected:
/// lines longer than <see cref="MaxLineLength"/>, unknown commands or properties, more than <see cref="MaxTargets"/>
/// or no targets, non-positive PIDs or creation times, negative ids.
/// </summary>
public static class AgentCommandReader
{
    /// <summary>The name of the close command.</summary>
    public const string CloseCommandName = "close";

    /// <summary>Maximum length of a command line in characters (4 KB).</summary>
    public const int MaxLineLength = 4096;

    /// <summary>Maximum number of targets of one command.</summary>
    public const int MaxTargets = 64;

    /// <summary>Parses one command line; returns <c>false</c> for anything that is not a valid close command.</summary>
    public static bool TryParse(string? line, [NotNullWhen(true)] out CloseCommand? command)
    {
        command = null;
        if (line is null || line.Length > MaxLineLength)
        {
            return false;
        }

        AgentProtocol.WireCommand? wire;
        try
        {
            wire = JsonSerializer.Deserialize(line, AgentProtocolJsonContext.Default.WireCommand);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is null || wire.Cmd != CloseCommandName || wire.Id < 0
            || wire.Targets.Length is 0 or > MaxTargets
            || wire.Targets.Any(t => t is null || t.Pid <= 0 || t.Created <= 0))
        {
            return false;
        }

        command = new CloseCommand(wire.Id, [.. wire.Targets.Select(t => new CloseTarget(t.Pid, t.Created))]);
        return true;
    }
}
