using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using static EagleEye.Service.Tests.Enforcement.EnforcementTestData;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class BlockedStartRunnerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 18, 10, 1, TimeSpan.Zero));
    private readonly Mock<IBlockedStartRepository> _repository = new();
    private readonly Mock<IKidMessenger> _messenger = new();
    private readonly FakeCloser _closer = new();
    private readonly TestLogger<BlockedStartLog> _logger = new();
    private readonly List<(long Id, string Outcome, double? Seconds, string Message)> _completed = [];
    private readonly BlockedStartRunner _runner;
    private long _nextId;

    public BlockedStartRunnerTests()
    {
        _repository.Setup(r => r.InsertAsync(It.IsAny<BlockedStartRecord>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Interlocked.Increment(ref _nextId));
        _repository.Setup(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<long, string, double?, string, DateTimeOffset, CancellationToken>((id, outcome, seconds, message, _, _) =>
            {
                lock (_completed)
                {
                    _completed.Add((id, outcome, seconds, message));
                }
            })
            .Returns(Task.CompletedTask);
        _messenger.Setup(m => m.ShowAsync(2, "Pause \U0001F60A", It.IsAny<CancellationToken>())).ReturnsAsync(KidMessageStates.Shown);
        _runner = new BlockedStartRunner(_repository.Object, _messenger.Object, _closer, new BlockedStartLog(_logger, new InstanceLogLimiter(_time)), _time);
    }

    public void Dispose() => _runner.Dispose();

    [Fact]
    public async Task Graceful_ClosedGracefullyRecordedAndLogged()
    {
        _closer.KillSet.Add(new FakeProcess(5120, 100) { ClosesGracefully = true });

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((1L, BlockedStartTexts.ClosedGracefully, (double?)0.0, KidMessageStates.Shown), _completed.Single());
        _repository.Verify(r => r.InsertAsync(It.Is<BlockedStartRecord>(b =>
            b.AccountSid == Kid && b.ProcessId == 5120 && b.Weekday == "Mo" && b.Trigger == BlockedStartTexts.AppStartTrigger && b.Entry == Entry), It.IsAny<CancellationToken>()), Times.Once);
        Assert.EndsWith("closed gracefully after 0.0 s; message shown.", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
        Assert.Equal(Start().Targets, _closer.CloseRequests.Single());
        Assert.All(_closer.Handles, h => Assert.True(h.Disposed));
    }

    [Fact]
    public async Task Graceful_AfterSomeSeconds_SecondsFromDetection()
    {
        var process = new FakeProcess(5120, 100);
        _closer.KillSet.Add(process);

        var handle = _runner.Start(Start());
        await RunUntil(() => _time.GetUtcNow() >= new DateTimeOffset(2026, 10, 12, 18, 10, 4, TimeSpan.Zero));
        process.Exited = true;
        await RunUntil(() => handle.IsCompleted);

        var (_, outcome, seconds, _) = _completed.Single();
        Assert.Equal(BlockedStartTexts.ClosedGracefully, outcome);
        Assert.InRange(seconds!.Value, 3.0, 3.5);
    }

    [Fact]
    public async Task StillRunningAt20s_TerminatedByForce()
    {
        var process = new FakeProcess(5120, 100);
        _closer.KillSet.Add(process);

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((BlockedStartTexts.TerminatedByForce, (double?)20.0), (_completed.Single().Outcome, _completed.Single().Seconds));
        Assert.Equal(1, process.Terminations);
        Assert.EndsWith("terminated by force after 20 s (20.0 s until gone); message shown.", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForceStep_RecomputesTheKillSet()
    {
        var root = new FakeProcess(5120, 100);
        _closer.KillSet.Add(root);
        var handle = _runner.Start(Start());
        await RunUntil(() => _closer.Handles.Count == 1);
        var child = new FakeProcess(6000, 300);
        _closer.KillSet.Add(child);

        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((1, 1), (root.Terminations, child.Terminations));
    }

    [Fact]
    public async Task AgentDoesNotAnswer_ForceStillEndsTheApp()
    {
        _closer.AgentAnswers = false;
        _closer.KillSet.Add(new FakeProcess(5120, 100) { ClosesGracefully = true });

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal(BlockedStartTexts.TerminatedByForce, _completed.Single().Outcome);
    }

    [Fact]
    public async Task NotEndedAfter3Rounds_ErrorAndNoSeconds()
    {
        var process = new FakeProcess(5120, 100) { TerminateError = 5 };
        _closer.KillSet.Add(process);

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((BlockedStartTexts.TerminatedByForce, (double?)null), (_completed.Single().Outcome, _completed.Single().Seconds));
        Assert.Equal(3, process.Terminations);
        Assert.Single(_logger.Messages(LogLevel.Error));
        Assert.Equal(3, _logger.Messages(LogLevel.Warning).Count(m => m.Contains("Win32 error 5", StringComparison.Ordinal)));
        Assert.EndsWith("(not ended after 3 rounds); message shown.", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddProcesses_CloseCommandAndCoveredBySequence_NoOwnRecord()
    {
        _closer.KillSet.Add(new FakeProcess(5120, 100) { ClosesGracefully = true });
        var second = new FakeProcess(7000, 500);
        _closer.KillSet.Add(second);
        var handle = _runner.Start(Start());
        await RunUntil(() => _closer.CloseRequests.Count == 1);

        handle.AddProcesses([new ObservedProcess(7000, 500), new ObservedProcess(7000, 500), new ObservedProcess(8000, 1)]);
        handle.AddProcesses([new ObservedProcess(7000, 500)]);
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal(3, _closer.CloseRequests.Count);
        Assert.Equal((BlockedStartTexts.TerminatedByForce, 1), (_completed.Single().Outcome, second.Terminations));
        Assert.Contains(_closer.ExtraRootsSeen, extra => extra.Contains((7000, 500)) && extra.Contains((8000, 1)));
        _repository.Verify(r => r.InsertAsync(It.IsAny<BlockedStartRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _messenger.Verify(m => m.ShowAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddProcesses_ReusedPidOfAnExitedProcess_Tracked()
    {
        var first = new FakeProcess(5120, 100);
        _closer.KillSet.Add(first);
        var handle = _runner.Start(Start());
        await RunUntil(() => _closer.Handles.Count == 1);
        first.Exited = true;
        var reused = new FakeProcess(5120, 900);
        _closer.KillSet.Add(reused);

        handle.AddProcesses([new ObservedProcess(5120, 900)]);
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((BlockedStartTexts.TerminatedByForce, 1), (_completed.Single().Outcome, reused.Terminations));
    }

    [Fact]
    public async Task NothingRunningAnymore_ClosedGracefullyAtOnce()
    {
        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((BlockedStartTexts.ClosedGracefully, (double?)0.0), (_completed.Single().Outcome, _completed.Single().Seconds));
    }

    [Theory]
    [InlineData(KidMessageStates.NotConnected)]
    [InlineData(KidMessageStates.AlreadyOpen)]
    public async Task MessageState_Recorded(string state)
    {
        _messenger.Setup(m => m.ShowAsync(2, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal(state, _completed.Single().Message);
    }

    [Fact]
    public async Task HistoryInsertFails_SequenceGoesOnWithoutRecord()
    {
        var failure = new InvalidOperationException("db");
        _repository.Setup(r => r.InsertAsync(It.IsAny<BlockedStartRecord>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var process = new FakeProcess(5120, 100);
        _closer.KillSet.Add(process);

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal((1, 0), (process.Terminations, _completed.Count));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Contains("Blocked start ended", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoryCompleteFails_WarningAndResultStillLogged()
    {
        var failure = new InvalidOperationException("db");
        _repository.Setup(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Contains("Blocked start ended", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StopAsync_RunningSequence_TerminatesAtOnceWithServiceStopping()
    {
        var process = new FakeProcess(5120, 100);
        _closer.KillSet.Add(process);
        var handle = _runner.Start(Start());
        await RunUntil(() => _closer.Handles.Count == 1);

        await _runner.StopAsync();

        Assert.True(handle.IsCompleted);
        Assert.Equal((BlockedStartTexts.TerminatedServiceStopping, (double?)null, 1), (_completed.Single().Outcome, _completed.Single().Seconds, process.Terminations));
        Assert.EndsWith("terminated by force (service stopping); message shown.", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnevenPolling_ForceStillExactlyAt20s()
    {
        _closer.KillSet.Add(new FakeProcess(5120, 100));

        var handle = _runner.Start(Start());
        await RunUntil(() => handle.IsCompleted, step: TimeSpan.FromMilliseconds(300));

        Assert.Equal(BlockedStartTexts.TerminatedByForce, _completed.Single().Outcome);
        Assert.InRange(_completed.Single().Seconds!.Value, 20.0, 20.3);
    }

    [Fact]
    public async Task CloseRequestCancelled_SequenceGoesOn()
    {
        _closer.CancelCloseRequests = true;
        _closer.KillSet.Add(new FakeProcess(5120, 100));

        var handle = _runner.Start(Start());
        await RunUntil(() => _closer.Handles.Count == 1);
        handle.AddProcesses([new ObservedProcess(9, 9)]);
        await RunUntil(() => handle.IsCompleted);

        Assert.Equal(BlockedStartTexts.TerminatedByForce, _completed.Single().Outcome);
    }

    [Fact]
    public async Task StopAsync_SequenceHangs_ReturnsAfterTheStopTimeout()
    {
        var never = new TaskCompletionSource<string>();
        _messenger.Setup(m => m.ShowAsync(2, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(never.Task);
        var handle = _runner.Start(Start());

        await _runner.StopAsync();

        Assert.False(handle.IsCompleted);
        never.SetResult(KidMessageStates.Shown);
        await RunUntil(() => handle.IsCompleted, advance: false);
    }

    [Fact]
    public async Task ParallelLimit_FurtherSequencesQueueAndAreDroppedAtStop()
    {
        _closer.KillSet.Add(new FakeProcess(5120, 100) { TerminateError = 5 });
        var handles = Enumerable.Range(0, BlockedStartRunner.MaxParallel + 1).Select(_ => _runner.Start(Start())).ToList();
        await RunUntil(() => Inserts() == BlockedStartRunner.MaxParallel, advance: false);
        await Task.Delay(50);
        var insertsWhileFull = Inserts();

        await _runner.StopAsync();

        Assert.Equal(BlockedStartRunner.MaxParallel, insertsWhileFull);
        Assert.All(handles, h => Assert.True(h.IsCompleted));
        Assert.Equal(BlockedStartRunner.MaxParallel, Inserts());
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _runner.Start(null!));
        var handle = _runner.Start(Start());
        Assert.Throws<ArgumentNullException>(() => handle.AddProcesses(null!));
    }

    private int Inserts() => _repository.Invocations.Count(i => i.Method.Name == nameof(IBlockedStartRepository.InsertAsync));

    private async Task RunUntil(Func<bool> condition, bool advance = true, TimeSpan? step = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(2);
            if (advance)
            {
                _time.Advance(step ?? TimeSpan.FromMilliseconds(250));
            }
        }

        Assert.True(condition());
    }
}
