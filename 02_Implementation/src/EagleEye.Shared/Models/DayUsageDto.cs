namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the state area "UsageDay:{AccountSid}:{Day}" (ADR-010 §3, ADR-012 §6).</summary>
/// <param name="Revision">Per-account counter, strictly increasing per day area during one service run.</param>
/// <param name="LastChangeRequestId">Always null (no client writes).</param>
/// <param name="AccountSid">The account.</param>
/// <param name="Day">Local date of the service PC.</param>
/// <param name="ServiceToday">The service PC's local date when the snapshot was made.</param>
/// <param name="Apps">Apps with usage on that day, in any order (the app sorts).</param>
public sealed record DayUsageDto(
    long Revision,
    Guid? LastChangeRequestId,
    string AccountSid,
    DateOnly Day,
    DateOnly ServiceToday,
    IReadOnlyList<AppUsageDto> Apps);
