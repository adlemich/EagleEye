namespace EagleEye.Shared.Models;

/// <summary>Snapshot of the global state area "AppGroups" (ADR-010, ADR-021 §5).</summary>
/// <param name="Revision">Strictly increasing during one service run.</param>
/// <param name="LastChangeRequestId">requestId of the write that produced this revision, or null.</param>
/// <param name="Groups">All groups, unsorted (the parent app sorts).</param>
/// <param name="Apps">The listed apps with their group, unsorted.</param>
public sealed record AppGroupsDto(long Revision, Guid? LastChangeRequestId, IReadOnlyList<AppGroupDto> Groups, IReadOnlyList<GroupableAppDto> Apps);
