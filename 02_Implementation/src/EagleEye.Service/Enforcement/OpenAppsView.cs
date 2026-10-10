using System.Collections.Immutable;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Thread-safe copy of the program paths each account has open (allowed apps), published by the accounting loop
/// after every event and read by close sequences when they compute a kill set (ADR-013 §5: processes of other open
/// apps are never terminated).
/// </summary>
public sealed class OpenAppsView
{
    private static readonly IReadOnlySet<string> None = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

    private ImmutableDictionary<string, IReadOnlySet<string>> _paths =
        ImmutableDictionary.Create<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Replaces the open paths of all accounts.</summary>
    public void Publish(IReadOnlyDictionary<string, IReadOnlySet<string>> openPaths)
    {
        ArgumentNullException.ThrowIfNull(openPaths);
        Volatile.Write(ref _paths, openPaths.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>The open program paths of the account (case-insensitive).</summary>
    public IReadOnlySet<string> Of(string accountSid)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        return Volatile.Read(ref _paths).TryGetValue(accountSid, out var paths) ? paths : None;
    }
}
