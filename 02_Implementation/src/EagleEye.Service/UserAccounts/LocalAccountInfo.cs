namespace EagleEye.Service.UserAccounts;

/// <summary>One local account of the service PC as Windows reports it.</summary>
/// <param name="Sid">The account's security identifier (string form), the identity (FR-SVC-074).</param>
/// <param name="UserName">The logon name.</param>
/// <param name="FullName">The full name, or <c>null</c> if empty.</param>
/// <param name="IsDisabled">Windows "Account is disabled".</param>
/// <param name="IsAdmin">Member of the local group Administrators, directly or through another group.</param>
public sealed record LocalAccountInfo(string Sid, string UserName, string? FullName, bool IsDisabled, bool IsAdmin);
