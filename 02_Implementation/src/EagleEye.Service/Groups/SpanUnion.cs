namespace EagleEye.Service.Groups;

/// <summary>The length of the union of intervals (TC-026, ADR-021 §3): overlapping or touching intervals count once. Pure.</summary>
public static class SpanUnion
{
    /// <summary>The whole seconds covered by at least one interval (rounded down).</summary>
    public static long Seconds(IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        var total = TimeSpan.Zero;
        DateTimeOffset? start = null;
        var end = DateTimeOffset.MinValue;
        foreach (var span in spans.OrderBy(s => s.Start))
        {
            if (start is null || span.Start > end)
            {
                total += end - (start ?? end);
                start = span.Start;
                end = span.End;
            }
            else if (span.End > end)
            {
                end = span.End;
            }
        }

        total += end - (start ?? end);
        return (long)total.TotalSeconds;
    }
}
