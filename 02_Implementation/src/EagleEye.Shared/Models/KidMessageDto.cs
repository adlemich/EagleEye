namespace EagleEye.Shared.Models;

/// <summary>A message for the kid dialog (US-006 AC-34, ADR-014 amendment, ADR-016).</summary>
/// <param name="Reason">Why the dialog is shown; decides the default text and the info line.</param>
/// <param name="ParentText">The parent's text for this reason, or null → the tray shows its default (ADR-016); line breaks "\n".</param>
/// <param name="AppName">Display name of the app, for app reasons.</param>
/// <param name="Minutes">Minutes left (rounded up, ≥ 1) for warnings and grace periods.</param>
/// <param name="BreakStartMinute">Start of the break entry (0 … 1439), for break reasons.</param>
/// <param name="BreakEndMinute">End of the break entry (1 … 1439; 1439 = midnight), for <see cref="KidMessageReason.BreakTime"/>.</param>
public sealed record KidMessageDto(
    KidMessageReason Reason,
    string? ParentText,
    string? AppName,
    int? Minutes,
    int? BreakStartMinute,
    int? BreakEndMinute);
