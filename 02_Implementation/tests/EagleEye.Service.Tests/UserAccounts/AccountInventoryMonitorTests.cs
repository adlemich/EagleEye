using System.Threading.Channels;
using EagleEye.Service.UserAccounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.UserAccounts;

public sealed class AccountInventoryMonitorTests : IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly TimerSignalingTimeProvider _time = new();
    private readonly Mock<IUserAccountService> _service = new();
    private readonly TestLogger<AccountInventoryMonitor> _logger = new();
    private readonly Channel<int> _calls = Channel.CreateUnbounded<int>();
    private readonly AccountInventoryMonitor _monitor;
    private int _count;

    public AccountInventoryMonitorTests()
    {
        _service.Setup(s => s.RefreshInventoryAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _calls.Writer.TryWrite(Interlocked.Increment(ref _count));
                return Task.CompletedTask;
            });
        _monitor = new AccountInventoryMonitor(_service.Object, _time, _logger);
    }

    public async Task InitializeAsync()
    {
        await _monitor.StartAsync(CancellationToken.None);

        // .NET 10 runs ExecuteAsync via Task.Run: advance the clock only after the monitor created its timer.
        await _time.TimerCreated.WaitAsync(Wait);
    }

    public async Task DisposeAsync()
    {
        await _monitor.StopAsync(CancellationToken.None);
        _monitor.Dispose();
    }

    [Fact]
    public void CheckInterval_Is15Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), AccountInventoryMonitor.CheckInterval);
    }

    [Fact]
    public async Task Execute_Every15Seconds_Refreshes()
    {
        _time.Advance(TimeSpan.FromSeconds(15));
        await NextCallAsync();
        _time.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(2, await NextCallAsync());
    }

    [Fact]
    public async Task Execute_Before15Seconds_DoesNotRefresh()
    {
        _time.Advance(TimeSpan.FromSeconds(14.9));
        await Task.Delay(50);

        Assert.Equal(0, Volatile.Read(ref _count));
    }

    [Fact]
    public async Task Execute_RefreshFails_LogsAndContinues()
    {
        var failure = new InvalidOperationException("boom");
        _service.SetupSequence(s => s.RefreshInventoryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Returns(() =>
            {
                _calls.Writer.TryWrite(Interlocked.Increment(ref _count));
                return Task.CompletedTask;
            });

        _time.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => _logger.Has(LogLevel.Error, failure));
        _time.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(1, await NextCallAsync());
    }

    [Fact]
    public async Task Stop_DuringRefresh_EndsWithoutErrorLog()
    {
        var started = new TaskCompletionSource();
        _service.Setup(s => s.RefreshInventoryAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });
        _time.Advance(TimeSpan.FromSeconds(15));
        await started.Task.WaitAsync(Wait);

        await _monitor.StopAsync(CancellationToken.None);

        Assert.Equal((true, 0), (_monitor.ExecuteTask!.IsCompletedSuccessfully, _logger.Entries.Count));
    }

    [Fact]
    public async Task Stop_WhileWaiting_Ends()
    {
        await _monitor.StopAsync(CancellationToken.None);

        // .NET 10 starts ExecuteAsync with Task.Run: a stop before it ran leaves the task canceled, never faulted.
        Assert.True(_monitor.ExecuteTask!.IsCompleted && !_monitor.ExecuteTask.IsFaulted);
    }

    private async Task<int> NextCallAsync() => await _calls.Reader.ReadAsync().AsTask().WaitAsync(Wait);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    /// <summary>A fake clock that signals when a timer is created.</summary>
    private sealed class TimerSignalingTimeProvider : FakeTimeProvider
    {
        private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TimerCreated => _timerCreated.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _timerCreated.TrySetResult();
            return timer;
        }
    }
}
