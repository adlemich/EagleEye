using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>A stored break-time entry of an account.</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="Entry">The entry.</param>
public sealed record StoredBreakTimeEntry(string AccountSid, BreakTimeEntryDto Entry);

/// <summary>A stored display text of an account (no row = default text).</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="Text">The text, line breaks "\n".</param>
public sealed record StoredDisplayText(string AccountSid, string Text);

/// <summary>Everything stored for the rules of all accounts.</summary>
/// <param name="Entries">All entries, in creation order.</param>
/// <param name="Texts">All changed display texts.</param>
public sealed record StoredRules(IReadOnlyList<StoredBreakTimeEntry> Entries, IReadOnlyList<StoredDisplayText> Texts);

/// <summary>What was deleted of the rules of an account that no longer exists (FR-SVC-047).</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="Entries">Deleted entries.</param>
/// <param name="HadText">Whether a display text was deleted.</param>
public sealed record RulesPurge(string AccountSid, int Entries, bool HadText);

/// <summary>A new row of the history of blocked starts (ADR-013 §7, AC-35).</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="UserName">The account's user name at the time.</param>
/// <param name="StartedUtc">Creation time of the app's first process.</param>
/// <param name="StartedLocal">The same, local time of the service PC.</param>
/// <param name="DetectedUtc">When the service detected the blocked start.</param>
/// <param name="DisplayName">The app's display name.</param>
/// <param name="ProcessName">The process name.</param>
/// <param name="ProgramPath">The program path.</param>
/// <param name="ProcessId">The reported window process.</param>
/// <param name="Trigger"><c>app start</c> or <c>found at agent start</c>.</param>
/// <param name="Entry">The entry in effect at the start.</param>
/// <param name="Weekday">The local weekday of the start, e.g. "Mo".</param>
public sealed record BlockedStartRecord(
    string AccountSid,
    string UserName,
    DateTimeOffset StartedUtc,
    DateTime StartedLocal,
    DateTimeOffset DetectedUtc,
    string DisplayName,
    string ProcessName,
    string ProgramPath,
    int ProcessId,
    string Trigger,
    BreakTimeEntryDto Entry,
    string Weekday);

/// <summary>A time-zone or clock change found by the service (ADR-013 §9, AC-37).</summary>
/// <param name="DetectedUtc">When it was found.</param>
/// <param name="DetectedLocal">The local time after the change.</param>
/// <param name="Kind"><c>TimeZone</c> or <c>Clock</c>.</param>
/// <param name="OldValue">The value before.</param>
/// <param name="NewValue">The value after.</param>
/// <param name="SessionId">The controlled session in use at the time, if exactly one.</param>
/// <param name="AccountSid">Its account.</param>
/// <param name="UserName">Its user name.</param>
public sealed record TimeChangeFinding(
    DateTimeOffset DetectedUtc,
    DateTime DetectedLocal,
    string Kind,
    string OldValue,
    string NewValue,
    int? SessionId,
    string? AccountSid,
    string? UserName);
