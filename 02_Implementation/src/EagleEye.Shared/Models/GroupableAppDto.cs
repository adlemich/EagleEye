namespace EagleEye.Shared.Models;

/// <summary>One app of the App Groups page: a program path with an app record in at least one controlled account (US-008 AC-3).</summary>
/// <param name="ProgramPath">The app's identity (case-insensitive); shown as a tooltip.</param>
/// <param name="DisplayName">As on Reports; of the most recently seen record when accounts differ.</param>
/// <param name="GroupId">The app's group, or <c>null</c> for "Not in any group".</param>
public sealed record GroupableAppDto(string ProgramPath, string DisplayName, long? GroupId);
