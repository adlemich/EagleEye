using EagleEye.Service.Enforcement;
using EagleEye.Service.Monitoring;
using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;
using Moq;
using Xunit;
using static EagleEye.Service.Tests.Enforcement.EnforcementTestData;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class BlockedAppCloserTests
{
    private const string Game = @"C:\Games\Game.exe";

    private readonly Mock<ISessionAgentSupervisor> _supervisor = new();
    private readonly Mock<IProcessTable> _table = new();
    private readonly Mock<IProcessTerminator> _terminator = new();
    private readonly OpenAppsView _openApps = new();
    private readonly BlockedAppCloser _closer;

    public BlockedAppCloserTests()
    {
        var policy = new ProgramPathPolicy(
            new ProgramPathRoots(@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)", @"C:\Program Files\EagleEye"),
            _ => DriveKind.Fixed);
        _closer = new BlockedAppCloser(_supervisor.Object, _table.Object, _terminator.Object, new EnforcementIgnoreList(@"C:\Windows"), policy, _openApps);
        _terminator.Setup(t => t.Open(It.IsAny<int>(), It.IsAny<long>())).Returns<int, long>((pid, _) => pid == 99 ? null : Mock.Of<IProcessHandle>(h => h.Pid == pid));
    }

    [Fact]
    public async Task RequestGracefulCloseAsync_SendsTheTargetsToTheSessionAgent()
    {
        var answer = new CloseAnswer(1, 1, 0);
        _supervisor.Setup(s => s.RequestCloseAsync(2, It.Is<IReadOnlyList<CloseTarget>>(t => t.Single() == new CloseTarget(5120, 100)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        Assert.Same(answer, await _closer.RequestGracefulCloseAsync(2, [new ObservedProcess(5120, 100)], CancellationToken.None));
    }

    [Fact]
    public void OpenKillSet_ExcludesExemptOpenAndEagleEyePrograms()
    {
        _table.Setup(t => t.Snapshot(2)).Returns(
        [
            new ProcessRow(5120, 1, 100, 2, Kid, Notepad),
            new ProcessRow(6000, 5120, 200, 2, Kid, Game),
            new ProcessRow(6001, 5120, 200, 2, Kid, @"C:\Windows\System32\conhost.exe"),
            new ProcessRow(6002, 5120, 200, 2, Kid, @"C:\Program Files\EagleEye\TrayClient\EagleEye.TrayClient.exe"),
            new ProcessRow(6003, 5120, 200, 2, Kid, @"C:\Games\Other.exe"),
            new ProcessRow(99, 5120, 200, 2, Kid, @"C:\Games\Gone.exe"),
        ]);
        _openApps.Publish(new Dictionary<string, IReadOnlySet<string>> { [Kid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Game } });

        var handles = _closer.OpenKillSet(Start(), new HashSet<(int, long)>());

        Assert.Equal([5120, 6003], handles.Select(h => h.Pid));
    }

    [Fact]
    public void Open_UsesPidAndCreationTime()
    {
        Assert.NotNull(_closer.Open(new ObservedProcess(7, 5)));
        _terminator.Verify(t => t.Open(7, 5), Times.Once);
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _closer.RequestGracefulCloseAsync(2, null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => _closer.OpenKillSet(null!, new HashSet<(int, long)>()));
        Assert.Throws<ArgumentNullException>(() => _closer.OpenKillSet(Start(), null!));
        Assert.Throws<ArgumentNullException>(() => _closer.Open(null!));
    }
}
