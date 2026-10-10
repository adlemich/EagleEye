namespace EagleEye.Shared.Models;

/// <summary>One break-time entry (TC-010, TC-011).</summary>
/// <param name="EntryId">Stable id; entries are listed in creation order (OQ-11).</param>
/// <param name="IsActive">The switch "On/Off" (AC-14).</param>
/// <param name="StartMinute">0 … 1438 (00:00 … 23:58).</param>
/// <param name="EndMinute">StartMinute + 1 … 1439; 1439 (23:59) means midnight (TC-011).</param>
/// <param name="Days">At least one day.</param>
public sealed record BreakTimeEntryDto(long EntryId, bool IsActive, int StartMinute, int EndMinute, BreakTimeDays Days);
