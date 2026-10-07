namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the state area "UserAccounts" (ADR-010 §3).</summary>
/// <param name="Revision">Strictly increasing during one service run (ADR-010 §4).</param>
/// <param name="LastChangeRequestId">The requestId of the write that produced this revision; null if the service produced it.</param>
/// <param name="Accounts">All standard accounts, ordered by user name (ordinal, ignore case). The app sorts for display.</param>
public sealed record UserAccountListDto(long Revision, Guid? LastChangeRequestId, IReadOnlyList<UserAccountDto> Accounts);
