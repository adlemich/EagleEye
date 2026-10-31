using EagleEye.Service.Data;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Groups;

/// <summary>
/// Group lines of a day (TC-026, US-008 AC-35, AC-36): per group, the union of the spans of the account's apps whose program
/// path is <b>currently</b> a member. A group is listed when its current members have spans that day; deleted groups and days
/// without spans (before 0.8.0) have no lines. Pure.
/// </summary>
public static class GroupUsageCalculator
{
    /// <summary>The group lines of the spans of one account and day.</summary>
    public static IReadOnlyList<GroupUsageDto> ForDay(IEnumerable<StoredSpan> spans, AppGroupsSnapshot groups)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(groups);
        return
        [
            .. spans
                .Select(span => (Span: span, Group: groups.GroupOf(span.ProgramPath)))
                .Where(x => x.Group is not null)
                .GroupBy(x => x.Group!.Value) // Filtered for null above.
                .Select(g => new GroupUsageDto(g.Key, groups.Names[g.Key], SpanUnion.Seconds(g.Select(x => (x.Span.StartUtc, x.Span.EndUtc))))),
        ];
    }
}
