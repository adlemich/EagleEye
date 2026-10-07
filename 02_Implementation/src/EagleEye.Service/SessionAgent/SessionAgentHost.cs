using System.Runtime.InteropServices;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Agent mode of the service executable (<c>EagleEye.Service.exe --session-agent</c>, ADR-011): scans the
/// windows of its session about once per second and writes report lines to stdout (on change, at least every
/// 5 s). End of file on stdin means "stop". No host, no database, no log file, no network, no window.
/// First action: <c>SetDefaultDllDirectories</c> (ADR-011 §7 item 7). Thin, verified manually.
/// </summary>
public static partial class SessionAgentHost
{
    /// <summary>The only accepted command-line argument.</summary>
    public const string Argument = "--session-agent";

    /// <summary>Time between two window scans.</summary>
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private const uint LoadLibrarySearchApplicationDir = 0x200;
    private const uint LoadLibrarySearchSystem32 = 0x800;

    /// <summary>Runs the agent until stdin is closed. Exit code 0 = stopped, 1 = error, 2 = invalid arguments.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        _ = SetDefaultDllDirectories(LoadLibrarySearchSystem32 | LoadLibrarySearchApplicationDir);
        using var stderr = new StreamWriter(Console.OpenStandardError(), AgentDiagnostics.StreamEncoding) { AutoFlush = true };
        if (args.Length != 1 || args[0] != Argument)
        {
            await stderr.WriteLineAsync("invalid arguments").ConfigureAwait(false);
            return 2;
        }

        try
        {
            await stderr.WriteLineAsync(AgentDiagnostics.Describe()).ConfigureAwait(false);
            await ScanUntilStoppedAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            // Agent boundary: report to the service (stderr) and exit; the supervisor restarts the agent.
            await stderr.WriteLineAsync($"{ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task ScanUntilStoppedAsync()
    {
        using var stop = new CancellationTokenSource();
        var stdin = new Thread(() => WaitForEndOfInput(stop)) { IsBackground = true, Name = "stdin" };
        stdin.Start();

        using var stdout = new StreamWriter(Console.OpenStandardOutput(), AgentDiagnostics.StreamEncoding) { AutoFlush = true, NewLine = "\n" };
        var enumerator = new WindowEnumerator();
        var publisher = new AgentReportPublisher(TimeProvider.System);
        using var timer = new PeriodicTimer(ScanInterval);
        do
        {
            var (windows, truncated) = enumerator.Enumerate();
            if (publisher.Next(AppWindowScanner.Scan(windows), truncated) is { } line)
            {
                await stdout.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
        while (await NextTickAsync(timer, stop.Token).ConfigureAwait(false));
    }

    private static async Task<bool> NextTickAsync(PeriodicTimer timer, CancellationToken stop)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stop).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static void WaitForEndOfInput(CancellationTokenSource stop)
    {
        try
        {
            using var stdin = Console.OpenStandardInput();
            var buffer = new byte[256];
            while (stdin.Read(buffer, 0, buffer.Length) > 0)
            {
                // The service sends no messages in US-004; anything read is ignored.
            }
        }
        catch (IOException)
        {
            // A broken pipe means the service is gone: stop as on end of file.
        }

        stop.Cancel();
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDefaultDllDirectories(uint directoryFlags);
}
