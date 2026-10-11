using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// A session agent started by <see cref="SessionAgentLauncher"/>: its process handle and the service's ends of the
/// three anonymous pipes. Thin Win32, verified manually.
/// </summary>
internal sealed partial class PipeAgentProcess : IAgentProcess
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly SafeProcessHandle _process;
    private readonly FileStream _stdin;
    private readonly FileStream _stdout;
    private readonly FileStream _stderr;
    private readonly BoundedLineReader _reports;
    private readonly BoundedLineReader _errors;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public PipeAgentProcess(int processId, SafeProcessHandle process, SafeFileHandle stdin, SafeFileHandle stdout, SafeFileHandle stderr)
    {
        ProcessId = processId;
        _process = process;
        _stdin = new FileStream(stdin, FileAccess.Write, 1, isAsync: false);
        _stdout = new FileStream(stdout, FileAccess.Read, 4096, isAsync: false);
        _stderr = new FileStream(stderr, FileAccess.Read, 4096, isAsync: false);
        _reports = new BoundedLineReader(_stdout);
        _errors = new BoundedLineReader(_stderr);
        Exited = Task.Factory.StartNew(WaitForExit, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public int ProcessId { get; }

    public Task<int> Exited { get; }

    public Task<string?> ReadReportLineAsync(CancellationToken ct) => _reports.ReadLineAsync(ct);

    public Task<string?> ReadErrorLineAsync(CancellationToken ct) => _errors.ReadLineAsync(ct);

    public async Task WriteLineAsync(string line, CancellationToken ct)
    {
        var bytes = SessionAgent.AgentDiagnostics.StreamEncoding.GetBytes(line + "\n");
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stdin.WriteAsync(bytes, ct).ConfigureAwait(false);
            await _stdin.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw new IOException("The session agent's input is closed.");
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        _writeGate.Release();
        await _stdin.DisposeAsync().ConfigureAwait(false);
        try
        {
            await Exited.WaitAsync(StopTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _ = TerminateProcess(_process, 1);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stdin.DisposeAsync().ConfigureAwait(false);
        await _stdout.DisposeAsync().ConfigureAwait(false);
        await _stderr.DisposeAsync().ConfigureAwait(false);
        _writeGate.Dispose();
        if (Exited.IsCompleted)
        {
            _process.Dispose();
        }
    }

    private int WaitForExit()
    {
        _ = WaitForSingleObject(_process, uint.MaxValue);
        return GetExitCodeProcess(_process, out var code) ? (int)code : -1;
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
