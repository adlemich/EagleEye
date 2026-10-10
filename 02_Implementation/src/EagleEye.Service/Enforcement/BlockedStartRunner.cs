using System.Collections.Concurrent;
using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// Runs one <see cref="Sequence"/> per blocked start (ADR-013 §4, US-005 Decision 4), at most
/// <see cref="MaxParallel"/> at a time (further ones queue): record and log at t₀; in parallel the kid's message and
/// the agent's graceful close; wait up to <see cref="GracefulTimeout"/> for every process of the kill set to exit;
/// otherwise up to <see cref="ForceRounds"/> rounds of "recompute the kill set, terminate, wait ≤ 1 s"; then the
/// result in the history and the log. Seconds count from t₀ (detection).
/// </summary>
public sealed class BlockedStartRunner(
    IBlockedStartRepository repository,
    IKidMessenger messenger,
    IBlockedAppCloser closer,
    BlockedStartLog log,
    TimeProvider timeProvider) : IBlockedStartRunner, IDisposable
{
    /// <summary>Time for the graceful close (FR-SVC-023 v1.5).</summary>
    public static readonly TimeSpan GracefulTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Wait after each termination round.</summary>
    public static readonly TimeSpan ForceWait = TimeSpan.FromSeconds(1);

    /// <summary>How often the sequence checks whether the processes have exited.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>How long a service stop waits for the sequences.</summary>
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Termination rounds before giving up.</summary>
    public const int ForceRounds = 3;

    /// <summary>Sequences running at the same time.</summary>
    public const int MaxParallel = 64;

    private readonly IBlockedStartRepository _repository = repository;
    private readonly IKidMessenger _messenger = messenger;
    private readonly IBlockedAppCloser _closer = closer;
    private readonly BlockedStartLog _log = log;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly SemaphoreSlim _parallel = new(MaxParallel, MaxParallel);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<Sequence, Task> _running = new();

    /// <inheritdoc />
    public IBlockedStartHandle Start(BlockedStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var sequence = new Sequence(this, start);
        _running[sequence] = sequence.RunAsync().ContinueWith(
            _ => _running.TryRemove(sequence, out Task? _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return sequence;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_running.Values).WaitAsync(StopTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Service stop must not hang; open records are completed with "unknown" at the next start.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _parallel.Dispose();
        _stopping.Dispose();
    }

    private static async Task IgnoreFailuresAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Service stopping: the force step of the sequence ends the app.
        }
    }

    /// <summary>The close sequence of one blocked start.</summary>
    private sealed class Sequence(BlockedStartRunner runner, BlockedStart start) : IBlockedStartHandle
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<int, IProcessHandle> _handles = [];
        private readonly HashSet<(int Pid, long Created)> _extra = [];
        private int _completed;

        public bool IsCompleted => Volatile.Read(ref _completed) == 1;

        public void AddProcesses(IReadOnlyList<ObservedProcess> processes)
        {
            ArgumentNullException.ThrowIfNull(processes);
            lock (_lock)
            {
                foreach (var process in processes.Where(p => _extra.Add((p.Pid, p.Created))))
                {
                    AddHandle(runner._closer.Open(process));
                }
            }

            _ = IgnoreFailuresAsync(runner._closer.RequestGracefulCloseAsync(start.SessionId, processes, runner._stopping.Token));
        }

        public async Task RunAsync()
        {
            var stopping = runner._stopping.Token;
            try
            {
                await runner._parallel.WaitAsync(stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Volatile.Write(ref _completed, 1);
                return;
            }

            try
            {
                await RunCoreAsync(stopping).ConfigureAwait(false);
            }
            finally
            {
                lock (_lock)
                {
                    foreach (var handle in _handles.Values)
                    {
                        handle.Dispose();
                    }
                }

                Volatile.Write(ref _completed, 1);
                runner._parallel.Release();
            }
        }

        private async Task RunCoreAsync(CancellationToken stopping)
        {
            var t0 = runner._timeProvider.GetTimestamp();
            var recordId = await InsertAsync().ConfigureAwait(false);
            var logged = runner._log.Detected(start);
            AddHandles(runner._closer.OpenKillSet(start, Extra()));
            var message = runner._messenger.ShowAsync(start.SessionId, start.DisplayText, CancellationToken.None);
            var close = IgnoreFailuresAsync(runner._closer.RequestGracefulCloseAsync(start.SessionId, start.Targets, stopping));
            var (outcome, seconds, logText) = await EndAsync(t0, stopping).ConfigureAwait(false);
            var messageState = await message.ConfigureAwait(false);
            await close.ConfigureAwait(false);
            await CompleteAsync(recordId, outcome, seconds, messageState).ConfigureAwait(false);
            runner._log.Ended(start, logText, messageState, logged);
        }

        private async Task<(string Outcome, double? Seconds, string LogText)> EndAsync(long t0, CancellationToken stopping)
        {
            try
            {
                while (!AllExited())
                {
                    var remaining = GracefulTimeout - runner._timeProvider.GetElapsedTime(t0);
                    if (remaining <= TimeSpan.Zero)
                    {
                        return await ForceAsync(t0, stopping).ConfigureAwait(false);
                    }

                    await Task.Delay(remaining < PollInterval ? remaining : PollInterval, runner._timeProvider, stopping).ConfigureAwait(false);
                }

                var seconds = Seconds(t0);
                return (BlockedStartTexts.ClosedGracefully, seconds, $"{BlockedStartTexts.ClosedGracefully} after {Format(seconds)} s");
            }
            catch (OperationCanceledException)
            {
                AddHandles(runner._closer.OpenKillSet(start, Extra()));
                TerminateRemaining();
                return (BlockedStartTexts.TerminatedServiceStopping, null, BlockedStartTexts.TerminatedServiceStopping);
            }
        }

        private async Task<(string Outcome, double? Seconds, string LogText)> ForceAsync(long t0, CancellationToken stopping)
        {
            for (var round = 0; round < ForceRounds; round++)
            {
                AddHandles(runner._closer.OpenKillSet(start, Extra()));
                TerminateRemaining();
                var roundStart = runner._timeProvider.GetTimestamp();
                while (!AllExited() && runner._timeProvider.GetElapsedTime(roundStart) < ForceWait)
                {
                    await Task.Delay(PollInterval, runner._timeProvider, stopping).ConfigureAwait(false);
                }

                if (AllExited())
                {
                    var seconds = Seconds(t0);
                    return (BlockedStartTexts.TerminatedByForce, seconds, $"{BlockedStartTexts.TerminatedByForce} after 20 s ({Format(seconds)} s until gone)");
                }
            }

            int remaining;
            lock (_lock)
            {
                remaining = _handles.Values.Count(h => !h.HasExited);
            }

            runner._log.NotEnded(start, remaining);
            return (BlockedStartTexts.TerminatedByForce, null, $"{BlockedStartTexts.TerminatedByForce} after 20 s (not ended after {ForceRounds} rounds)");
        }

        private void TerminateRemaining()
        {
            List<IProcessHandle> alive;
            lock (_lock)
            {
                alive = [.. _handles.Values.Where(h => !h.HasExited)];
            }

            foreach (var handle in alive)
            {
                if (handle.Terminate() is var error and not 0)
                {
                    runner._log.TerminateFailed(start, handle.Pid, error);
                }
            }
        }

        private async Task<long?> InsertAsync()
        {
            var record = new BlockedStartRecord(
                start.AccountSid, start.UserName, start.StartedUtc, start.StartedLocal, start.DetectedUtc, start.DisplayName,
                start.ProcessName, start.ProgramPath, start.Targets[0].Pid, start.Trigger, start.Entry,
                BreakTimeChangeLog.Weekday(start.StartedLocal.DayOfWeek));
            try
            {
                return await runner._repository.InsertAsync(record, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // History boundary: the app must be closed even if the record cannot be stored.
                runner._log.HistoryFailed(start, ex);
                return null;
            }
        }

        private async Task CompleteAsync(long? recordId, string outcome, double? seconds, string messageState)
        {
            if (recordId is not { } id)
            {
                return;
            }

            try
            {
                await runner._repository.CompleteAsync(id, outcome, seconds, messageState, runner._timeProvider.GetUtcNow(), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // History boundary: the result is still logged.
                runner._log.HistoryFailed(start, ex);
            }
        }

        private void AddHandles(IReadOnlyList<IProcessHandle> handles)
        {
            lock (_lock)
            {
                foreach (var handle in handles)
                {
                    AddHandle(handle);
                }
            }
        }

        private void AddHandle(IProcessHandle? handle)
        {
            if (handle is null)
            {
                return;
            }

            if (_handles.TryGetValue(handle.Pid, out var existing) && !existing.HasExited)
            {
                handle.Dispose();
                return;
            }

            // New, or the PID was reused by another process of the kill set after the old one exited.
            existing?.Dispose();
            _handles[handle.Pid] = handle;
        }

        private IReadOnlySet<(int Pid, long Created)> Extra()
        {
            lock (_lock)
            {
                return _extra.ToHashSet();
            }
        }

        private bool AllExited()
        {
            lock (_lock)
            {
                return _handles.Values.All(h => h.HasExited);
            }
        }

        private double Seconds(long t0) => Math.Round(runner._timeProvider.GetElapsedTime(t0).TotalSeconds, 1);

        private static string Format(double seconds) => seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
