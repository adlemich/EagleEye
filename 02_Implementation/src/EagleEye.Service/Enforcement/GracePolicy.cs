namespace EagleEye.Service.Enforcement;

/// <summary>
/// The grace-period rule (ADR-015 §5, US-006 Decision 7, Michael's answer to plan Q-2: <b>minimum grace</b>). After a parent
/// change of the account's app rules or break times, a running app gets a grace period of the full final-warning minutes
/// when the change moved its end earlier and less than the final-warning minutes are left. Pure.
/// </summary>
public static class GracePolicy
{
    private const double SecondsPerMinute = 60;

    /// <summary>The grace threshold and length in seconds: the final-warning minutes.</summary>
    public static double Threshold(int finalMinutes) => finalMinutes * SecondsPerMinute;

    /// <summary>Whether a grace period starts.</summary>
    /// <param name="newLeft">Seconds left with the new rules (<see cref="double.PositiveInfinity"/> = no end within the horizon).</param>
    /// <param name="oldLeft">Seconds left with the old rules, at the same moment.</param>
    /// <param name="finalMinutes">The final-warning minutes.</param>
    public static bool Starts(double newLeft, double oldLeft, int finalMinutes) => newLeft < Threshold(finalMinutes) && newLeft < oldLeft;
}
