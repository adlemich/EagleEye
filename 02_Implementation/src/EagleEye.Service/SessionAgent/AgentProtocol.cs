using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// The agent's report line (ADR-011 §3): one JSON object per line, e.g.
/// <c>{"seq":17,"truncated":false,"apps":[{"pid":4711,"kind":"Window"},{"pid":812,"kind":"StoreApp","host":640}]}</c>.
/// Parsing is strict, because the service treats the line as untrusted input (ADR-011 §7 item 11): unknown or
/// duplicate properties, missing fields, wrong types, nesting deeper than 4, more than <see cref="MaxApps"/>
/// entries, non-positive PIDs and unknown kinds are rejected.
/// </summary>
public static class AgentProtocol
{
    /// <summary>Maximum number of apps in one report.</summary>
    public const int MaxApps = 1000;

    /// <summary>Serializes a report as one line (without the line break).</summary>
    public static string Serialize(AgentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var wire = new WireReport(
            report.Seq,
            report.Truncated,
            [.. report.Apps.Select(a => new WireApp(a.Pid, a.Kind.ToString(), a.HostPid))]);
        return JsonSerializer.Serialize(wire, AgentProtocolJsonContext.Default.WireReport);
    }

    /// <summary>Parses one line; returns <c>false</c> for anything that is not a valid report.</summary>
    public static bool TryParse(string? line, [NotNullWhen(true)] out AgentReport? report)
    {
        report = null;
        WireReport? wire;
        try
        {
            wire = line is null ? null : JsonSerializer.Deserialize(line, AgentProtocolJsonContext.Default.WireReport);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is null || wire.Seq < 0 || wire.Apps.Length > MaxApps)
        {
            return false;
        }

        var apps = new List<AgentApp>(wire.Apps.Length);
        foreach (var entry in wire.Apps)
        {
            if (ToApp(entry) is not { } app)
            {
                return false;
            }

            apps.Add(app);
        }

        report = new AgentReport(wire.Seq, wire.Truncated, apps);
        return true;
    }

    private static AgentApp? ToApp(WireApp entry)
    {
        if (entry.Pid <= 0)
        {
            return null;
        }

        return (entry.Kind, entry.Host) switch
        {
            (nameof(AgentAppKind.Window), null) => new AgentApp(entry.Pid, AgentAppKind.Window),
            (nameof(AgentAppKind.FileExplorer), null) => new AgentApp(entry.Pid, AgentAppKind.FileExplorer),
            (nameof(AgentAppKind.ExplorerWindow), null) => new AgentApp(entry.Pid, AgentAppKind.ExplorerWindow),
            (nameof(AgentAppKind.StoreApp), > 0) => new AgentApp(entry.Pid, AgentAppKind.StoreApp, entry.Host),
            _ => null,
        };
    }

    /// <summary>Serializes a close command of the service as one line (ADR-013 §4).</summary>
    public static string SerializeCloseCommand(CloseCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var wire = new WireCommand(AgentCommandReader.CloseCommandName, command.Id, [.. command.Targets.Select(t => new WireTarget(t.Pid, t.Created))]);
        return JsonSerializer.Serialize(wire, AgentProtocolJsonContext.Default.WireCommand);
    }

    /// <summary>Serializes the agent's answer to a close command as one line.</summary>
    public static string SerializeCloseAnswer(CloseAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var wire = new WireAnswer(new WireClosed(answer.Id, answer.Windows, answer.Missing));
        return JsonSerializer.Serialize(wire, AgentProtocolJsonContext.Default.WireAnswer);
    }

    /// <summary>Whether a stdout line of the agent is a close answer rather than a report.</summary>
    public static bool IsCloseAnswer(string? line) => line is not null && line.StartsWith("{\"closed\":", StringComparison.Ordinal);

    /// <summary>Parses the agent's answer to a close command strictly; <c>false</c> for anything else.</summary>
    public static bool TryParseCloseAnswer(string? line, [NotNullWhen(true)] out CloseAnswer? answer)
    {
        answer = null;
        WireAnswer? wire;
        try
        {
            wire = line is null ? null : JsonSerializer.Deserialize(line, AgentProtocolJsonContext.Default.WireAnswer);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is null || wire.Closed is not { Id: >= 0, Windows: >= 0, Missing: >= 0 } closed)
        {
            return false;
        }

        answer = new CloseAnswer(closed.Id, closed.Windows, closed.Missing);
        return true;
    }

    /// <summary>Wire form of a close command.</summary>
    internal sealed record WireCommand(string Cmd, long Id, WireTarget[] Targets);

    /// <summary>Wire form of a close target.</summary>
    internal sealed record WireTarget(int Pid, long Created);

    /// <summary>Wire form of a close answer line.</summary>
    internal sealed record WireAnswer(WireClosed Closed);

    /// <summary>Wire form of the content of a close answer.</summary>
    internal sealed record WireClosed(long Id, int Windows, int Missing);

    /// <summary>Wire form of a report.</summary>
    internal sealed record WireReport(long Seq, bool Truncated, WireApp[] Apps);

    /// <summary>Wire form of an app entry; <c>host</c> only for Store apps.</summary>
    internal sealed record WireApp(int Pid, string Kind, int? Host = null);
}

/// <summary>Source-generated, strict JSON settings of the agent protocol.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    MaxDepth = 4)]
[JsonSerializable(typeof(AgentProtocol.WireReport))]
[JsonSerializable(typeof(AgentProtocol.WireCommand))]
[JsonSerializable(typeof(AgentProtocol.WireAnswer))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serialization code; the protocol rules are tested through AgentProtocol.")]
internal sealed partial class AgentProtocolJsonContext : JsonSerializerContext
{
}
