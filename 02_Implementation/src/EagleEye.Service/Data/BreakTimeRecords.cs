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

/// <summary>The kinds of a history record (US-006 migration 5, ADR-015 §7).</summary>
public static class CloseKinds
{
    /// <summary>A start that was blocked (break time or app rule).</summary>
    public const string BlockedStart = "blocked start";

    /// <summary>A running app that was closed (budget end, not allowed, midnight, break begin).</summary>
    public const string RunningApp = "running app";
}

/// <summary>The reasons of a history record (US-006 migration 5, ADR-015 §6, §7).</summary>
public static class CloseReasons
{
    /// <summary>A break time is in effect.</summary>
    public const string BreakTime = "break time";

    /// <summary>The app is not allowed today (day not ticked or budget 0:00), also at midnight.</summary>
    public const string NotAllowedToday = "not allowed today";

    /// <summary>The app's budget is used up.</summary>
    public const string BudgetUsedUp = "budget used up";
}

/// <summary>The app rule of a closed app at the moment of the close (history columns of migration 5).</summary>
/// <param name="BudgetMinutes">The budget.</param>
/// <param name="Days">The allowed weekdays.</param>
/// <param name="UsedSeconds">The used time today at that moment.</param>
public sealed record AppRuleAtClose(int BudgetMinutes, BreakTimeDays Days, long UsedSeconds);

/// <summary>A new row of the history of blocked starts and enforced closes (ADR-013 §7, ADR-015 §7, US-005 AC-35, US-006 AC-37).</summary>
/// <param name="AccountSid">The account.</param>
/// <param name="UserName">The account's user name at the time.</param>
/// <param name="StartedUtc">Creation time of the app's first process.</param>
/// <param name="StartedLocal">The same, local time of the service PC.</param>
/// <param name="DetectedUtc">When the service detected the blocked start.</param>
/// <param name="DisplayName">The app's display name.</param>
/// <param name="ProcessName">The process name.</param>
/// <param name="ProgramPath">The program path.</param>
/// <param name="ProcessId">The reported window process.</param>
/// <param name="Trigger"><c>app start</c>, <c>found at agent start</c>, or the trigger of a running-app close.</param>
/// <param name="Entry">The break entry (reason "break time"), else <c>null</c>.</param>
/// <param name="Weekday">The local weekday, e.g. "Mo".</param>
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
    BreakTimeEntryDto? Entry,
    string Weekday)
{
    /// <summary>One of <see cref="CloseKinds"/>.</summary>
    public string Kind { get; init; } = CloseKinds.BlockedStart;

    /// <summary>One of <see cref="CloseReasons"/>.</summary>
    public string Reason { get; init; } = CloseReasons.BreakTime;

    /// <summary>Whether the close followed a grace period (US-006 AC-32, AC-33).</summary>
    public bool AfterGrace { get; init; }

    /// <summary>The app rule at that moment (budget reasons), else <c>null</c>.</summary>
    public AppRuleAtClose? Rule { get; init; }
}

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
