using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Which warnings were shown (ADR-015 §4, US-006 Decision 6). Keys: per account, app and local day for app ends; per
/// session and break occurrence for break ends. Showing the final warning also marks the early warning. Shown levels are
/// never re-armed that day, also when a parent raises a budget (plan Q-5). In memory only (ADR-015 §9). Pure.
/// </summary>
public sealed class WarningLedger
{
    private readonly Dictionary<WarningKey, WarningLevel> _shown = [];

    /// <summary>The key of an app end (budget, not allowed today, midnight).</summary>
    public static WarningKey AppKey(string accountSid, string programPath, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(accountSid);
        ArgumentNullException.ThrowIfNull(programPath);
        return new WarningKey($"app|{accountSid.ToUpperInvariant()}|{programPath.ToUpperInvariant()}", day, null);
    }

    /// <summary>The key of a break occurrence in a session.</summary>
    public static WarningKey BreakKey(int sessionId, DateTime breakStartLocal) =>
        new($"break|{sessionId}", DateOnly.FromDateTime(breakStartLocal), breakStartLocal.TimeOfDay);

    /// <summary>Whether <paramref name="level"/> was shown for the key (a shown final warning counts for the early one too).</summary>
    public bool IsShown(WarningKey key, WarningLevel level) =>
        _shown.TryGetValue(key, out var shown) && (shown == WarningLevel.Final || shown == level);

    /// <summary>Marks the level shown (final also marks early).</summary>
    public void MarkShown(WarningKey key, WarningLevel level)
    {
        if (!IsShown(key, level))
        {
            _shown[key] = level;
        }
    }

    /// <summary>Forgets the keys of days before <paramref name="today"/>.</summary>
    public void Prune(DateOnly today)
    {
        foreach (var key in _shown.Keys.ToList())
        {
            if (key.Day < today)
            {
                _shown.Remove(key);
            }
        }
    }
}

/// <summary>A key of the <see cref="WarningLedger"/>.</summary>
/// <param name="Scope">The app or the session.</param>
/// <param name="Day">The local day (of the break start for breaks).</param>
/// <param name="BreakStart">The break start time of day, or <c>null</c> for apps.</param>
public sealed record WarningKey(string Scope, DateOnly Day, TimeSpan? BreakStart);
