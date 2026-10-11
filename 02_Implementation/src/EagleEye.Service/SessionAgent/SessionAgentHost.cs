using System.Runtime.InteropServices;
using System.Threading.Channels;
using EagleEye.Service.Monitoring;

namespace EagleEye.Service.SessionAgent;

/// <summary>
/// Agent mode of the service executable (<c>EagleEye.Service.exe --session-agent</c>, ADR-011): scans the
/// windows of its session about once per second and writes report lines to stdout (on change, at least every
/// 5 s). Stdin carries the service's close commands (US-005, ADR-011 amendment): each is handled between two scans
/// by <see cref="WindowCloser"/> and answered on stdout. End of file on stdin means "stop". No host, no database,
/// no log file, no network, no window.
/// First action: <c>SetDefaultDllDirectories</c> (ADR-011 §7 item 7). Thin, verified manually.
/// </summary>
public static partial class SessionAgentHost
{
    /// <summary>The only accepted command-line argument.</summary>
    public const string Argument = "--session-agent";

    /// <summary>Time between two window scans.</summary>
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private const int CommandQueueLength = 16;

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
            await ScanUntilStoppedAsync(TextWriter.Synchronized(stderr)).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            // Agent boundary: report to the service (stderr) and exit; the supervisor restarts the agent.
            await stderr.WriteLineAsync($"{ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task ScanUntilStoppedAsync(TextWriter stderr)
    {
        using var stop = new CancellationTokenSource();
        var commands = Channel.CreateBounded<CloseCommand>(new BoundedChannelOptions(CommandQueueLength) { SingleWriter = true });
        _ = Task.Run(() => ReadCommandsAsync(commands.Writer, stderr, stop));

        using var stdout = new StreamWriter(Console.OpenStandardOutput(), AgentDiagnostics.StreamEncoding) { AutoFlush = true, NewLine = "\n" };
        var enumerator = new WindowEnumerator();
        var closer = new WindowCloser(enumerator);
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
        while (await WaitForTickHandlingCommandsAsync(timer, commands.Reader, closer, stdout, stop.Token).ConfigureAwait(false));
    }

    /// <summary>Waits for the next scan; close commands that arrive meanwhile are handled at once (between scans).</summary>
    private static async Task<bool> WaitForTickHandlingCommandsAsync(
        PeriodicTimer timer, ChannelReader<CloseCommand> commands, WindowCloser closer, StreamWriter stdout, CancellationToken stop)
    {
        var tick = NextTickAsync(timer, stop);
        while (true)
        {
            var command = WaitForCommandAsync(commands, stop);
            if (await Task.WhenAny(tick, command).ConfigureAwait(false) == tick)
            {
                return await tick.ConfigureAwait(false);
            }

            while (commands.TryRead(out var next))
            {
                await stdout.WriteLineAsync(AgentProtocol.SerializeCloseAnswer(closer.Close(next))).ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> WaitForCommandAsync(ChannelReader<CloseCommand> commands, CancellationToken stop)
    {
        try
        {
            return await commands.WaitToReadAsync(stop).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
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

    /// <summary>
    /// Reads command lines from stdin (bounded, strict, coding guidelines §12.4). Invalid lines are reported on stderr
    /// and ignored; an over-long or broken line, end of file or a broken pipe stop the agent.
    /// </summary>
    private static async Task ReadCommandsAsync(ChannelWriter<CloseCommand> commands, TextWriter stderr, CancellationTokenSource stop)
    {
        try
        {
            await using var stdin = Console.OpenStandardInput();
            var reader = new BoundedLineReader(stdin, AgentCommandReader.MaxLineLength);
            while (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line)
            {
                if (AgentCommandReader.TryParse(line, out var command))
                {
                    await commands.WriteAsync(command, stop.Token).ConfigureAwait(false);
                }
                else
                {
                    await stderr.WriteLineAsync("invalid command ignored").ConfigureAwait(false);
                }
            }
        }
        catch (AgentProtocolException ex)
        {
            await stderr.WriteLineAsync($"command channel closed: {ex.Message}").ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A broken pipe means the service is gone: stop as on end of file.
        }

        commands.TryComplete();
        await stop.CancelAsync().ConfigureAwait(false);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDefaultDllDirectories(uint directoryFlags);
}
