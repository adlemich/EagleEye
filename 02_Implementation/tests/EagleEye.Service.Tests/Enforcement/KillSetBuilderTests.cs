using EagleEye.Service.Enforcement;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class KillSetBuilderTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
    private const string Launcher = @"C:\Games\Launcher.exe";
    private const string Game = @"C:\Games\Game.exe";
    private const string Helper = @"C:\Games\Helper.exe";

    private static readonly IReadOnlySet<string> NoOpen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<(int, long)> NoExtra = new HashSet<(int, long)>();

    [Fact]
    public void Build_AllProcessesOfThePathInTheSession()
    {
        ProcessRow[] table = [Row(10, 1, 100, Edge), Row(11, 10, 101, Edge), Row(12, 999, 50, Edge.ToUpperInvariant()), Row(13, 1, 100, Game)];

        Assert.Equal([10, 11, 12], Pids(KillSetBuilder.Build(table, Request(Edge), _ => false)));
    }

    [Fact]
    public void Build_DescendantsIncludingGrandchildren()
    {
        ProcessRow[] table = [Row(10, 1, 100, Launcher), Row(20, 10, 110, Game), Row(30, 20, 120, Helper), Row(40, 1, 90, Helper)];

        Assert.Equal([10, 20, 30], Pids(KillSetBuilder.Build(table, Request(Launcher), _ => false)));
    }

    [Fact]
    public void Build_ChildOlderThanParent_PidReuseIgnored()
    {
        ProcessRow[] table = [Row(10, 1, 100, Launcher), Row(20, 10, 99, Game)];

        Assert.Equal([10], Pids(KillSetBuilder.Build(table, Request(Launcher), _ => false)));
    }

    [Fact]
    public void Build_OtherSessionOrOwner_Excluded()
    {
        ProcessRow[] table = [Row(10, 1, 100, Edge), Row(11, 1, 100, Edge) with { SessionId = 3 }, Row(12, 1, 100, Edge) with { OwnerSid = "S-1-5-18" },
            Row(13, 10, 110, Helper) with { OwnerSid = null }];

        Assert.Equal([10], Pids(KillSetBuilder.Build(table, Request(Edge), _ => false)));
    }

    [Fact]
    public void Build_ExemptProgramsAndTheirChildren_Excluded()
    {
        ProcessRow[] table = [Row(10, 1, 100, Launcher), Row(20, 10, 110, @"C:\Windows\System32\conhost.exe"), Row(30, 20, 120, Helper)];

        Assert.Equal([10], Pids(KillSetBuilder.Build(table, Request(Launcher), path => path.Contains("conhost", StringComparison.Ordinal))));
    }

    [Fact]
    public void Build_OtherOpenAppAndItsDescendants_Excluded()
    {
        // A game started from the launcher before the break is an open app: it keeps running (AC-25).
        ProcessRow[] table = [Row(10, 1, 100, Launcher), Row(20, 10, 110, Game), Row(30, 20, 120, Helper), Row(40, 10, 130, Helper)];
        var request = Request(Launcher) with { OpenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Game.ToUpperInvariant() } };

        Assert.Equal([10, 40], Pids(KillSetBuilder.Build(table, request, _ => false)));
    }

    [Fact]
    public void Build_ExtraRootsAndTheirDescendants()
    {
        ProcessRow[] table = [Row(10, 1, 100, Edge), Row(50, 1, 200, Game), Row(51, 50, 210, Helper), Row(52, 1, 200, Game)];
        var request = Request(Edge) with { ExtraRoots = new HashSet<(int, long)> { (50, 200), (52, 999) } };

        Assert.Equal([10, 50, 51], Pids(KillSetBuilder.Build(table, request, _ => false)));
    }

    [Fact]
    public void Build_NothingRunning_Empty()
    {
        Assert.Empty(KillSetBuilder.Build([], Request(Edge), _ => false));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => KillSetBuilder.Build(null!, Request(Edge), _ => false));
        Assert.Throws<ArgumentNullException>(() => KillSetBuilder.Build([], null!, _ => false));
        Assert.Throws<ArgumentNullException>(() => KillSetBuilder.Build([], Request(Edge), null!));
    }

    private static KillSetRequest Request(string path) => new(2, Kid, path, NoExtra, NoOpen);

    private static ProcessRow Row(int pid, int parent, long created, string path) => new(pid, parent, created, 2, Kid, path);

    private static int[] Pids(IReadOnlyList<ProcessRow> rows) => [.. rows.Select(r => r.Pid)];
}
