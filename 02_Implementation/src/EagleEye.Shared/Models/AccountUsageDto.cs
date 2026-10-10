namespace EagleEye.Shared.Models;

/// <summary>
/// Result of <c>IParentHub.GetAccountUsage</c>: today (always present, possibly without apps) plus
/// every day of the last 90 days with usage, newest first (ADR-012 §6).
/// </summary>
/// <param name="AccountSid">The account.</param>
/// <param name="ServiceToday">The service PC's local date.</param>
/// <param name="Days">The days, newest first.</param>
public sealed record AccountUsageDto(string AccountSid, DateOnly ServiceToday, IReadOnlyList<DayUsageDto> Days);
