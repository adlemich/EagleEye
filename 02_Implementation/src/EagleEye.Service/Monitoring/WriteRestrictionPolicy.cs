namespace EagleEye.Service.Monitoring;

/// <summary>
/// Decides whether agents are started with the write-restricted token (ADR-011 §7 item 1, Q-9). The launcher
/// starts with the restriction. If the first agents exit before writing any report (the .NET runtime or the
/// desktop connection does not start under the write-restricted token) and no agent has ever worked with it,
/// the launcher falls back to the token without the write restriction; everything else stays.
/// </summary>
public sealed class WriteRestrictionPolicy
{
    /// <summary>Early exits in a row after which the fallback is used.</summary>
    public const int EarlyExitsBeforeFallback = 2;

    private int _earlyExits;
    private bool _everWorked;

    /// <summary>Whether the next agent gets the write-restricted token.</summary>
    public bool UseWriteRestriction { get; private set; } = true;

    /// <summary>An agent wrote a report: the restriction works and is never dropped.</summary>
    public void ReportWorking() => _everWorked = true;

    /// <summary>An agent exited before its first report. Returns <c>true</c> when this switched to the fallback.</summary>
    public bool ReportEarlyExit()
    {
        if (!UseWriteRestriction || _everWorked)
        {
            return false;
        }

        UseWriteRestriction = ++_earlyExits < EarlyExitsBeforeFallback;
        return !UseWriteRestriction;
    }
}
