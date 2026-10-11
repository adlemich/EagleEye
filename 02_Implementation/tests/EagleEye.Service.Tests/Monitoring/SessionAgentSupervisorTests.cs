using System.Threading.Channels;
using EagleEye.Service.Monitoring;
using EagleEye.Service.SessionAgent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class SessionAgentSupervisorTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Report = """{"seq":1,"truncated":false,"apps":[{"pid":5,"kind":"Window"}]}""";

    private static readonly SessionInfo KidSession = new(2, Kid, SessionConnectState.Active, false);
    private static readonly Dictionary<string, string> Controlled = new() { [Kid] = "kid1" };

    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IAgentLauncher> _launcher = new();
    private readonly Mock<IAgentReportSink> _sink = new();
    private readonly TestLogger<SessionAgentSupervisor> _logger = new();
    private readonly List<FakeAgent> _agents = [];
    private readonly SessionAgentSupervisor _supervisor;

    public SessionAgentSupervisorTests()
    {
        _launcher.Setup(l => l.Launch(It.IsAny<int>())).Returns(() =>
        {
            var agent = new FakeAgent(100 + _agents.Count);
            _agents.Add(agent);
            return agent;
        });
        _supervisor = new SessionAgentSupervisor(_launcher.Object, _sink.Object, _time, _logger);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _supervisor.StopAllAsync();

    [Fact]
    public async Task Reconcile_ControlledSession_StartsAgentAndLogs()
    {
        await ReconcileAsync();

        _launcher.Verify(l => l.Launch(2), Times.Once);
        Assert.Contains("Session agent started in session 2 (process 100).", Info());
        Assert.Contains($"Usage recording started for account kid1 ({Kid}) in session 2.", Info());
    }

    [Fact]
    public async Task Reconcile_RunningAgent_NotStartedAgain()
    {
        await ReconcileAsync();
        await ReconcileAsync();

        _launcher.Verify(l => l.Launch(2), Times.Once);
    }

    [Fact]
    public async Task Report_IsPassedToSinkAndMarksObserving()
    {
        await ReconcileAsync();
        var before = _supervisor.IsObserving(2);

        await _agents[0].ReportAsync(Report);
        await WaitUntil(() => _supervisor.IsObserving(2));

        Assert.False(before);
        _sink.Verify(s => s.Process(2, Kid, It.IsAny<long>(), It.Is<AgentReport>(r => r.Apps.Single().Pid == 5)), Times.Once);
        _launcher.Verify(l => l.ReportWorking(), Times.Once);
    }

    [Fact]
    public async Task SecondReport_ReportWorkingOnlyOnce()
    {
        await ReconcileAsync();
        await _agents[0].ReportAsync(Report);
        await _agents[0].ReportAsync(Report);
        await WaitUntil(() => _sink.Invocations.Count == 2);

        _launcher.Verify(l => l.ReportWorking(), Times.Once);
    }

    [Fact]
    public void IsObserving_UnknownSession_False()
    {
        Assert.False(_supervisor.IsObserving(7));
    }

    [Fact]
    public async Task MissingHeartbeat_PausesAndRestartsAfterBackOff()
    {
        await StartReportingAsync();
        _time.Advance(TimeSpan.FromSeconds(16));
        Assert.False(_supervisor.IsObserving(2));

        await ReconcileAsync();
        var stopped = _agents[0].Stopped;
        await ReconcileAsync();
        var launchesBeforeBackOff = _agents.Count;
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();

        Assert.Equal((true, 1, 2), (stopped, launchesBeforeBackOff, _agents.Count));
        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.Contains("no report for 15 s", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exit_LogsWarningWithCodeAndRestarts()
    {
        await StartReportingAsync();
        _agents[0].Exit(3);

        await ReconcileAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();

        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.Contains("exited with code 3", StringComparison.Ordinal));
        Assert.Equal(2, _agents.Count);
        _launcher.Verify(l => l.ReportEarlyExit(), Times.Never);
    }

    [Fact]
    public async Task ExitBeforeFirstReport_ReportsEarlyExit()
    {
        await ReconcileAsync();
        _agents[0].Exit(1);

        await ReconcileAsync();

        _launcher.Verify(l => l.ReportEarlyExit(), Times.Once);
    }

    [Fact]
    public async Task InvalidReport_RestartsAgent()
    {
        await ReconcileAsync();
        await _agents[0].ReportAsync("{garbage");

        await ReconcileUntilStoppedAsync(_agents[0]);

        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.Contains("sent an invalid report", StringComparison.Ordinal));
        _sink.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverlongLine_RestartsAgent()
    {
        await ReconcileAsync();
        await _agents[0].FailAsync(new AgentProtocolException("too long"));

        await ReconcileUntilStoppedAsync(_agents[0]);

        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.Contains("too long", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadFailure_LoggedAndRestarts()
    {
        var failure = new IOException("pipe broken");
        await ReconcileAsync();
        await _agents[0].FailAsync(failure);

        await ReconcileUntilStoppedAsync(_agents[0]);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public async Task FourFailuresInARow_ErrorOnceThenEvery30Seconds()
    {
        await ReconcileAsync();
        for (var i = 0; i < 5; i++)
        {
            _agents[^1].Exit(1);
            await ReconcileAsync();
            _time.Advance(TimeSpan.FromSeconds(30));
            await ReconcileAsync();
        }

        Assert.Single(_logger.Messages(LogLevel.Error));
        Assert.Contains($"Usage recording for account kid1 ({Kid}) is not possible", _logger.Messages(LogLevel.Error)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulReport_ResetsFailureStreak()
    {
        await ReconcileAsync();
        for (var i = 0; i < 3; i++)
        {
            _agents[^1].Exit(1);
            await ReconcileAsync();
            _time.Advance(TimeSpan.FromSeconds(30));
            await ReconcileAsync();
        }

        await _agents[^1].ReportAsync(Report);
        await WaitUntil(() => _supervisor.IsObserving(2));
        _agents[^1].Exit(1);
        await ReconcileAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();

        Assert.Empty(_logger.Messages(LogLevel.Error));
        Assert.Equal(5, _agents.Count);
    }

    [Fact]
    public async Task Untick_StopsAgent()
    {
        await ReconcileAsync();

        await _supervisor.ReconcileAsync([KidSession], new Dictionary<string, string>());

        Assert.True(_agents[0].Stopped && _agents[0].Disposed);
        Assert.Contains("Session agent in session 2 stopped.", Info());
    }

    [Fact]
    public async Task LaunchFails_WarnsAndRetriesAfterBackOff()
    {
        var failure = new System.ComponentModel.Win32Exception(5);
        _launcher.SetupSequence(l => l.Launch(2)).Throws(failure).Returns(new FakeAgent(200));

        await ReconcileAsync();
        await ReconcileAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        _launcher.Verify(l => l.Launch(2), Times.Exactly(2));
    }

    [Fact]
    public async Task FailedSlotOfEndedSession_IsForgotten()
    {
        await ReconcileAsync();
        _agents[0].Exit(1);
        await ReconcileAsync();

        await _supervisor.ReconcileAsync([], Controlled);
        await ReconcileAsync();

        Assert.Equal(2, _logger.Messages(LogLevel.Information).Count(m => m.StartsWith("Usage recording started", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task FailedSlotOfStillEligibleSession_IsKept()
    {
        await ReconcileAsync();
        _agents[0].Exit(1);
        await ReconcileAsync();

        await ReconcileAsync();

        Assert.Single(_agents);
    }

    [Fact]
    public async Task ErrorOutput_DiagnosticsAtInformationOthersAtWarning()
    {
        await ReconcileAsync();

        await _agents[0].ErrorAsync("diagnostics: user SYSTEM, integrity System");
        await _agents[0].ErrorAsync("Win32Exception: boom");
        await WaitUntil(() => _logger.Messages(LogLevel.Warning).Count == 1);

        Assert.Contains("Session agent in session 2: diagnostics: user SYSTEM, integrity System", Info());
        Assert.Equal("Session agent in session 2: Win32Exception: boom", _logger.Messages(LogLevel.Warning).Single());
    }

    [Fact]
    public async Task ErrorOutputFails_Logged()
    {
        var failure = new AgentProtocolException("too long");
        await ReconcileAsync();

        await _agents[0].FailErrorsAsync(failure);
        await WaitUntil(() => _logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public async Task StopAll_StopsAgentsAndEndsReading()
    {
        await ReconcileAsync();

        await _supervisor.StopAllAsync();

        Assert.True(_agents[0].Stopped);
        await WaitUntil(() => _agents[0].ReadsDone);
    }

    [Fact]
    public async Task ReportOfReplacedAgent_IsIgnoredForHealth()
    {
        await ReconcileAsync();
        var old = _agents[0];
        old.CloseOnExit = false;
        old.Exit(1);
        await ReconcileAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();

        await old.ReportAsync(Report);
        await old.ReportAsync("{garbage");
        await _agents[1].ReportAsync(Report);
        await WaitUntil(() => _supervisor.IsObserving(2));

        Assert.True(_supervisor.IsObserving(2));
        _sink.Verify(s => s.Process(2, Kid, It.IsAny<long>(), It.IsAny<AgentReport>()), Times.Once);
    }

    // ---------- Close commands (US-005 Decision 5) ----------

    [Fact]
    public async Task RequestClose_WritesCommandAndReturnsTheCorrelatedAnswer()
    {
        await StartReportingAsync();

        var request = _supervisor.RequestCloseAsync(2, [new CloseTarget(4711, 1234)]);
        await WaitUntil(() => _agents[0].Written.Count == 1);
        await _agents[0].ReportAsync("""{"closed":{"id":99,"windows":5,"missing":0}}""");
        await _agents[0].ReportAsync("""{"closed":{"id":1,"windows":2,"missing":0}}""");

        Assert.Equal(new CloseAnswer(1, 2, 0), await request);
        Assert.Equal("""{"cmd":"close","id":1,"targets":[{"pid":4711,"created":1234}]}""", _agents[0].Written.Single());
    }

    [Fact]
    public async Task RequestClose_NoAnswer_NullAfterTimeout()
    {
        await StartReportingAsync();

        var request = _supervisor.RequestCloseAsync(2, [new CloseTarget(4711, 1234)]);
        await WaitUntil(() => _agents[0].Written.Count == 1);
        _time.Advance(SessionAgentSupervisor.CloseAnswerTimeout);

        Assert.Null(await request);
    }

    [Fact]
    public async Task RequestClose_NoAgentOrNoTargets_NullAtOnce()
    {
        Assert.Null(await _supervisor.RequestCloseAsync(2, [new CloseTarget(1, 1)]));
        await StartReportingAsync();
        Assert.Null(await _supervisor.RequestCloseAsync(2, []));
        Assert.Empty(_agents[0].Written);
    }

    [Fact]
    public async Task RequestClose_WriteFails_Null()
    {
        await StartReportingAsync();
        _agents[0].WriteFailure = new IOException("broken pipe");

        Assert.Null(await _supervisor.RequestCloseAsync(2, [new CloseTarget(1, 1)]));
    }

    [Fact]
    public async Task RequestClose_Cancelled_Throws()
    {
        await StartReportingAsync();
        using var cancel = new CancellationTokenSource();
        var request = _supervisor.RequestCloseAsync(2, [new CloseTarget(1, 1)], cancel.Token);
        await WaitUntil(() => _agents[0].Written.Count == 1);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task AnswerLine_CountsAsHeartbeat()
    {
        await StartReportingAsync();
        _time.Advance(TimeSpan.FromSeconds(14));
        await _agents[0].ReportAsync("""{"closed":{"id":5,"windows":0,"missing":1}}""");
        await Task.Delay(50);
        _time.Advance(TimeSpan.FromSeconds(2));

        await ReconcileAsync();

        Assert.True(_supervisor.IsObserving(2));
        Assert.Single(_agents);
    }

    [Fact]
    public async Task InvalidAnswerLine_FaultAndRestart()
    {
        await StartReportingAsync();
        await _agents[0].ReportAsync("""{"closed":{"id":-1,"windows":0,"missing":0}}""");
        await WaitUntil(() => !_supervisor.IsObserving(2));

        await ReconcileAsync();

        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.Contains("sent an invalid answer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentRun_ChangesOnRestart()
    {
        var runs = new List<long>();
        _sink.Setup(s => s.Process(2, Kid, It.IsAny<long>(), It.IsAny<AgentReport>())).Callback<int, string, long, AgentReport>((_, _, run, _) => runs.Add(run));
        await StartReportingAsync();
        _agents[0].Exit(1);
        await ReconcileAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ReconcileAsync();
        await _agents[1].ReportAsync(Report);
        await WaitUntil(() => runs.Count == 2);

        Assert.NotEqual(runs[0], runs[1]);
    }

    [Fact]
    public async Task RequestClose_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _supervisor.RequestCloseAsync(2, null!));
    }

    [Fact]
    public async Task Guards_ThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _supervisor.ReconcileAsync(null!, Controlled));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _supervisor.ReconcileAsync([], null!));
    }

    private Task ReconcileAsync() => _supervisor.ReconcileAsync([KidSession], Controlled);

    private async Task ReconcileUntilStoppedAsync(FakeAgent agent)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!agent.Stopped && DateTime.UtcNow < deadline)
        {
            await ReconcileAsync();
            await Task.Delay(5);
        }

        Assert.True(agent.Stopped);
    }

    private async Task StartReportingAsync()
    {
        await ReconcileAsync();
        await _agents[0].ReportAsync(Report);
        await WaitUntil(() => _supervisor.IsObserving(2));
    }

    private List<string> Info() => [.. _logger.Messages(LogLevel.Information)];

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(condition());
    }

    private sealed class FakeAgent(int processId) : IAgentProcess
    {
        private readonly Channel<object> _reports = Channel.CreateUnbounded<object>();
        private readonly Channel<object> _errors = Channel.CreateUnbounded<object>();
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readsDone;

        public int ProcessId { get; } = processId;

        public Task<int> Exited => _exited.Task;

        public bool Stopped { get; private set; }

        public bool CloseOnExit { get; set; } = true;

        public bool Disposed { get; private set; }

        public bool ReadsDone => Volatile.Read(ref _readsDone) > 0;

        public async Task ReportAsync(string line) => await _reports.Writer.WriteAsync(line);

        public async Task ErrorAsync(string line) => await _errors.Writer.WriteAsync(line);

        public async Task FailAsync(Exception failure) => await _reports.Writer.WriteAsync(failure);

        public async Task FailErrorsAsync(Exception failure) => await _errors.Writer.WriteAsync(failure);

        public void Exit(int code)
        {
            _exited.TrySetResult(code);
            if (CloseOnExit)
            {
                _reports.Writer.TryComplete();
                _errors.Writer.TryComplete();
            }
        }

        public List<string> Written { get; } = [];

        public Exception? WriteFailure { get; set; }

        public Task WriteLineAsync(string line, CancellationToken ct)
        {
            lock (Written)
            {
                Written.Add(line);
            }

            return WriteFailure is null ? Task.CompletedTask : Task.FromException(WriteFailure);
        }

        public Task<string?> ReadReportLineAsync(CancellationToken ct) => ReadAsync(_reports, ct, countDone: true);

        public Task<string?> ReadErrorLineAsync(CancellationToken ct) => ReadAsync(_errors, ct, countDone: false);

        public Task StopAsync()
        {
            Stopped = true;
            Exit(0);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        private async Task<string?> ReadAsync(Channel<object> channel, CancellationToken ct, bool countDone)
        {
            try
            {
                if (!await channel.Reader.WaitToReadAsync(ct))
                {
                    MarkDone(countDone);
                    return null;
                }

                var item = await channel.Reader.ReadAsync(ct);
                if (item is Exception failure)
                {
                    MarkDone(countDone);
                    throw failure;
                }

                if (((string)item).StartsWith('{') && ((string)item).Contains("garbage", StringComparison.Ordinal))
                {
                    MarkDone(countDone);
                }

                return (string)item;
            }
            catch (OperationCanceledException)
            {
                MarkDone(countDone);
                throw;
            }
        }

        private void MarkDone(bool countDone)
        {
            if (countDone)
            {
                Interlocked.Increment(ref _readsDone);
            }
        }
    }
}
