namespace EagleEye.Service.Enforcement;

/// <summary>What a kill set is computed for (ADR-013 §5).</summary>
/// <param name="SessionId">The session of the blocked start.</param>
/// <param name="OwnerSid">The account.</param>
/// <param name="ProgramPath">The blocked app's program path.</param>
/// <param name="ExtraRoots">Processes added to the running sequence (PID + creation time).</param>
/// <param name="OpenPaths">Program paths of the account's other open (allowed) apps.</param>
public sealed record KillSetRequest(
    int SessionId, string OwnerSid, string ProgramPath, IReadOnlySet<(int Pid, long Created)> ExtraRoots, IReadOnlySet<string> OpenPaths);

/// <summary>
/// Pure kill-set computation (ADR-013 §5, story AC-22 "with all its processes", AC-23, AC-25): roots are the
/// processes of the program path (and the added processes) in the session, owned by the account; descendants are
/// added recursively when created after their parent (PID-reuse guard). Never in the set: other sessions or owners,
/// exempt programs (<paramref name="isExempt"/>: EagleEye, ignore list incl. Explorer and ApplicationFrameHost),
/// and processes of other open apps of the account — with their descendants, which are only reachable through them.
/// </summary>
public static class KillSetBuilder
{
    /// <summary>The processes to terminate.</summary>
    public static IReadOnlyList<ProcessRow> Build(IReadOnlyList<ProcessRow> table, KillSetRequest request, Func<string, bool> isExempt)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(isExempt);

        bool Eligible(ProcessRow row) =>
            row.SessionId == request.SessionId
            && StringComparer.OrdinalIgnoreCase.Equals(row.OwnerSid, request.OwnerSid)
            && !isExempt(row.ImagePath)
            && !request.OpenPaths.Contains(row.ImagePath);

        var set = new Dictionary<int, ProcessRow>();
        var queue = new Queue<ProcessRow>();
        foreach (var row in table.Where(r => Eligible(r)
            && (StringComparer.OrdinalIgnoreCase.Equals(r.ImagePath, request.ProgramPath) || request.ExtraRoots.Contains((r.Pid, r.Created)))))
        {
            set[row.Pid] = row;
            queue.Enqueue(row);
        }

        while (queue.TryDequeue(out var parent))
        {
            foreach (var child in table)
            {
                if (child.ParentPid == parent.Pid && child.Created > parent.Created && !set.ContainsKey(child.Pid) && Eligible(child))
                {
                    set[child.Pid] = child;
                    queue.Enqueue(child);
                }
            }
        }

        return [.. set.Values.OrderBy(r => r.Pid)];
    }
}
