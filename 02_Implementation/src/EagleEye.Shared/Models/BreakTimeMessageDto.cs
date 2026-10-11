namespace EagleEye.Shared.Models;

/// <summary>The message for the kid's tray client after a blocked start (ADR-014).</summary>
/// <param name="DisplayText">The account's display text at the moment of the blocked start (AC-31); line breaks "\n".</param>
public sealed record BreakTimeMessageDto(string DisplayText);
