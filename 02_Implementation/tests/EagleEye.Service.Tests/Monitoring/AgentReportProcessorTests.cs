using EagleEye.Service.Monitoring;
using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class AgentReportProcessorTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const int Session = 2;

    private static readonly ProgramPathRoots Roots = new(@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)", @"C:\Program Files\EagleEye");

    private readonly Dictionary<int, ProcessFacts> _processes = [];
    private readonly Mock<IProcessInspector> _inspector = new();
    private readonly Mock<IAppMetadataSource> _metadata = new();
    private readonly UsageEventQueue _queue = new();
    private readonly FakeTimeProvider _time = new();
    private readonly TestLogger<AgentReportProcessor> _logger = new();
    private readonly AgentReportProcessor _processor;

    public AgentReportProcessorTests()
    {
        _inspector.Setup(i => i.GetCreationTime(It.IsAny<int>())).Returns((int pid) => _processes.TryGetValue(pid, out var p) ? p.CreationTime : null);
        _inspector.Setup(i => i.Inspect(It.IsAny<int>())).Returns((int pid) => _processes.GetValueOrDefault(pid));
        _metadata.Setup(m => m.Read(It.IsAny<ProcessFacts>())).Returns(AppMetadata.None);
        var policy = new ProgramPathPolicy(Roots, _ => DriveKind.Fixed);
        _processor = new AgentReportProcessor(
            _inspector.Object, new AppNameResolver(_metadata.Object, new TestLogger<AppNameResolver>()), policy, _queue, _time, _logger);
    }

    [Fact]
    public async Task Process_OwnApp_IsObservedWithNameAndPid()
    {
        AddProcess(10, @"C:\Windows\System32\notepad.exe");
        _metadata.Setup(m => m.Read(It.IsAny<ProcessFacts>())).Returns(new AppMetadata(null, "Editor"));

        var apps = await ProcessAsync(Window(10));

        Assert.Equal([(@"C:\Windows\System32\notepad.exe", "notepad.exe", "Editor", 10)], apps.Select(a => (a.ProgramPath, a.ProcessName, a.DisplayName, a.Processes.Single().Pid)));
    }

    [Fact]
    public async Task Process_EnqueuesForSessionAndAccount()
    {
        AddProcess(10, @"C:\Windows\notepad.exe");

        _processor.Process(Session, Kid, 1, new AgentReport(1, false, [Window(10)]));
        var observed = (AppsObserved)await _queue.DequeueAsync(CancellationToken.None);

        Assert.Equal((Session, Kid, 1L), (observed.SessionId, observed.AccountSid, observed.AgentRun));
    }

    [Fact]
    public async Task Process_OtherSession_Dropped()
    {
        AddProcess(10, @"C:\a\game.exe", session: 3);

        Assert.Empty(await ProcessAsync(Window(10)));
    }

    [Theory]
    [InlineData("S-1-5-21-1-2-3-1001")]
    [InlineData(null)]
    public async Task Process_OtherOwner_Dropped(string? owner)
    {
        AddProcess(10, @"C:\a\game.exe", owner: owner);

        Assert.Empty(await ProcessAsync(Window(10)));
    }

    [Fact]
    public async Task Process_ProcessGone_Dropped()
    {
        Assert.Empty(await ProcessAsync(Window(77)));
    }

    [Fact]
    public async Task Process_InspectionFails_Dropped()
    {
        AddProcess(10, @"C:\a\game.exe");
        _inspector.Setup(i => i.Inspect(10)).Returns((ProcessFacts?)null);

        Assert.Empty(await ProcessAsync(Window(10)));
    }

    [Fact]
    public async Task Process_ProcessReplacedBetweenCalls_Dropped()
    {
        AddProcess(10, @"C:\a\game.exe");
        _inspector.Setup(i => i.Inspect(10)).Returns(_processes[10] with { CreationTime = 999 });

        Assert.Empty(await ProcessAsync(Window(10)));
    }

    [Fact]
    public async Task Process_EagleEyeProgramInInstallFolder_Dropped()
    {
        AddProcess(10, @"C:\Program Files\EagleEye\TrayClient\EagleEye.TrayClient.exe");

        Assert.Empty(await ProcessAsync(Window(10)));
    }

    [Fact]
    public async Task Process_RenamedCopyWithEagleEyeName_IsRecorded()
    {
        AddProcess(10, @"C:\Users\kid\EagleEye.TrayClient.exe");

        Assert.Single(await ProcessAsync(Window(10)));
    }

    [Fact]
    public async Task Process_StoreAppBehindRealFrameHost_IsTheApp()
    {
        AddProcess(640, @"C:\Windows\System32\ApplicationFrameHost.exe");
        AddProcess(812, @"C:\Program Files\WindowsApps\Calc_1_x64__abc\CalculatorApp.exe");

        var apps = await ProcessAsync(new AgentApp(812, AgentAppKind.StoreApp, 640));

        Assert.Equal("CalculatorApp.exe", apps.Single().ProcessName);
    }

    [Fact]
    public async Task Process_StoreAppBehindFakeFrame_CountsTheFrameProcess()
    {
        AddProcess(640, @"C:\Users\kid\fakeframe.exe");
        AddProcess(812, @"C:\Windows\System32\notepad.exe");

        var apps = await ProcessAsync(new AgentApp(812, AgentAppKind.StoreApp, 640));

        Assert.Equal("fakeframe.exe", apps.Single().ProcessName);
    }

    [Fact]
    public async Task Process_StoreAppFrameNotOwn_Dropped()
    {
        AddProcess(812, @"C:\Program Files\WindowsApps\Calc_1_x64__abc\CalculatorApp.exe");

        Assert.Empty(await ProcessAsync(new AgentApp(812, AgentAppKind.StoreApp, 640)));
    }

    [Fact]
    public async Task Process_StoreAppItselfNotOwn_Dropped()
    {
        AddProcess(640, @"C:\Windows\System32\ApplicationFrameHost.exe");
        AddProcess(812, @"C:\Program Files\WindowsApps\Calc_1_x64__abc\CalculatorApp.exe", owner: "S-1-5-18");

        Assert.Empty(await ProcessAsync(new AgentApp(812, AgentAppKind.StoreApp, 640)));
    }

    [Theory]
    [InlineData(@"C:\Windows\explorer.exe", AgentAppKind.FileExplorer, 1)]
    [InlineData(@"C:\Windows\explorer.exe", AgentAppKind.ExplorerWindow, 0)]
    [InlineData(@"C:\Users\kid\explorer.exe", AgentAppKind.FileExplorer, 1)]
    [InlineData(@"C:\Users\kid\explorer.exe", AgentAppKind.ExplorerWindow, 1)]
    public async Task Process_ExplorerKinds_DependOnRealPath(string path, AgentAppKind kind, int expected)
    {
        AddProcess(10, path);

        Assert.Equal(expected, (await ProcessAsync(new AgentApp(10, kind))).Count);
    }

    [Fact]
    public async Task Process_ExplorerWindowOfGoneProcess_Dropped()
    {
        Assert.Empty(await ProcessAsync(new AgentApp(10, AgentAppKind.ExplorerWindow)));
    }

    [Fact]
    public async Task Process_TwoProcessesSamePath_OneAppWithBoth()
    {
        AddProcess(10, @"C:\a\Code.exe");
        AddProcess(11, @"C:\A\code.exe", created: 5);

        var apps = await ProcessAsync(Window(11), Window(10));

        Assert.Equal([new ObservedProcess(10, 1), new ObservedProcess(11, 5)], apps.Single().Processes);
    }

    [Fact]
    public async Task Process_SamePidTwiceInOneReport_InspectedOnce()
    {
        AddProcess(10, @"C:\a\Code.exe");

        await ProcessAsync(Window(10), new AgentApp(10, AgentAppKind.FileExplorer));

        _inspector.Verify(i => i.Inspect(10), Times.Once);
    }

    [Fact]
    public async Task Process_SameProcessInNextReport_NotInspectedAgain()
    {
        AddProcess(10, @"C:\a\game.exe");
        await ProcessAsync(Window(10));

        await ProcessAsync(Window(10));

        _inspector.Verify(i => i.Inspect(10), Times.Once);
    }

    [Fact]
    public async Task Process_PidReused_InspectedAgain()
    {
        AddProcess(10, @"C:\a\game.exe");
        await ProcessAsync(Window(10));
        AddProcess(10, @"C:\a\other.exe", created: 2);

        var apps = await ProcessAsync(Window(10));

        Assert.Equal("other.exe", apps.Single().ProcessName);
    }

    [Fact]
    public async Task Process_Truncated_WarnsOncePerHour()
    {
        await ProcessAsync(true);
        await ProcessAsync(true);
        _time.Advance(AgentReportProcessor.TruncationWarningInterval);
        await ProcessAsync(true);

        Assert.Equal(2, _logger.Messages(LogLevel.Warning).Count);
    }

    [Fact]
    public async Task Process_InspectorThrows_LoggedAndSkipped()
    {
        var failure = new InvalidOperationException("boom");
        _inspector.Setup(i => i.GetCreationTime(10)).Throws(failure);
        _processor.Process(Session, Kid, 1, new AgentReport(1, false, [Window(10)]));
        AddProcess(11, @"C:\a\game.exe");

        Assert.Single(await ProcessAsync(Window(11)));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public void Process_Guards_Throw()
    {
        Assert.ThrowsAny<ArgumentException>(() => _processor.Process(Session, " ", 1, new AgentReport(1, false, [])));
        Assert.Throws<ArgumentNullException>(() => _processor.Process(Session, Kid, 1, null!));
    }

    private static AgentApp Window(int pid) => new(pid, AgentAppKind.Window);

    private void AddProcess(int pid, string path, int session = Session, string? owner = Kid, long created = 1)
    {
        _processes[pid] = new ProcessFacts(pid, session, owner, created, path, null);
    }

    private Task<IReadOnlyList<ObservedApp>> ProcessAsync(params AgentApp[] apps) => ProcessAsync(false, apps);

    private async Task<IReadOnlyList<ObservedApp>> ProcessAsync(bool truncated, params AgentApp[] apps)
    {
        _processor.Process(Session, Kid, 1, new AgentReport(1, truncated, apps));
        return ((AppsObserved)await _queue.DequeueAsync(CancellationToken.None)).Apps;
    }
}
