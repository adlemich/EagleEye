namespace EagleEye.Shared.Models;

/// <summary>A standard account of the service PC (never an admin or built-in account).</summary>
/// <param name="Sid">Windows security identifier, e.g. "S-1-5-21-…-1001". The identity (FR-SVC-074).</param>
/// <param name="UserName">The logon name, e.g. "max".</param>
/// <param name="FullName">The full name as set in Windows, or null if empty.</param>
/// <param name="IsDisabled">Windows "Account is disabled" (AC-12).</param>
/// <param name="IsUnderParentalControl">The stored selection (default false, AC-18).</param>
public sealed record UserAccountDto(string Sid, string UserName, string? FullName, bool IsDisabled, bool IsUnderParentalControl);
