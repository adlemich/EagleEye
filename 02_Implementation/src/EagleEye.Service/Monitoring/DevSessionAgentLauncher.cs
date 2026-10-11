#if DEBUG
using System.Diagnostics;
using EagleEye.Service.SessionAgent;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// <b>Debug builds only</b> (ADR-011 T-13): starts the agent as an ordinary child process in the current session, so
/// DEV can smoke-test without SYSTEM rights. Used when <see cref="DevSettings.WatchSidVariable"/> is set. Never
/// compiled into Release builds; the installers package Release.
/// </summary>
public sealed class DevSessionAgentLauncher : IAgentLauncher
{
    /// <inheritdoc />
    public IAgentProcess Launch(int sessionId)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) // The service always has a process path.
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = AgentDiagnostics.StreamEncoding,
        };
        start.ArgumentList.Add(SessionAgentHost.Argument);
        return new DevAgentProcess(Process.Start(start)!); // Process.Start returns a process for a non-shell start.
    }

    /// <inheritdoc />
    public void ReportEarlyExit()
    {
        // No token variants in Debug mode.
    }

    /// <inheritdoc />
    public void ReportWorking()
    {
        // No token variants in Debug mode.
    }

    private sealed class DevAgentProcess : IAgentProcess
    {
        private readonly Process _process;
        private readonly BoundedLineReader _reports;
        private readonly BoundedLineReader _errors;
        private readonly SemaphoreSlim _writeGate = new(1, 1);

        public DevAgentProcess(Process process)
        {
            _process = process;
            _reports = new BoundedLineReader(process.StandardOutput.BaseStream);
            _errors = new BoundedLineReader(process.StandardError.BaseStream);
            Exited = WaitAsync();
        }

        public int ProcessId => _process.Id;

        public Task<int> Exited { get; }

        public Task<string?> ReadReportLineAsync(CancellationToken ct) => _reports.ReadLineAsync(ct);

        public Task<string?> ReadErrorLineAsync(CancellationToken ct) => _errors.ReadLineAsync(ct);

        public async Task WriteLineAsync(string line, CancellationToken ct)
        {
            await _writeGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _process.StandardInput.WriteAsync((line + "\n").AsMemory(), ct).ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public async Task StopAsync()
        {
            _process.StandardInput.Close();
            try
            {
                await Exited.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _process.Kill();
            }
        }

        public ValueTask DisposeAsync()
        {
            _process.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<int> WaitAsync()
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
            return _process.ExitCode;
        }
    }
}
#endif
