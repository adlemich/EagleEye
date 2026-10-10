using EagleEye.Service.Enforcement;
using EagleEye.Service.SessionAgent;
using EagleEye.Service.Statistics;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Tests.Enforcement;

/// <summary>Test data and fakes of the enforcement tests.</summary>
internal static class EnforcementTestData
{
    public const string Kid = "S-1-5-21-1-2-3-1003";
    public const string Notepad = @"C:\Windows\System32\notepad.exe";

    public static readonly BreakTimeEntryDto Entry = new(3, true, 1200, 1439, BreakTimeDays.All);

    public static BlockedStart Start(params ObservedProcess[] targets) => new(
        2, Kid, "kid1", Notepad, "notepad.exe", "Editor", targets.Length == 0 ? [new ObservedProcess(5120, 100)] : targets,
        BlockedStartTexts.AppStartTrigger, Entry, "Pause \U0001F60A", new DateTimeOffset(2026, 10, 12, 18, 10, 0, TimeSpan.Zero),
        new DateTime(2026, 10, 12, 20, 10, 0), new DateTimeOffset(2026, 10, 12, 18, 10, 1, TimeSpan.Zero));
}

/// <summary>A process of the fake process world.</summary>
internal sealed class FakeProcess(int pid, long created)
{
    public int Pid { get; } = pid;

    public long Created { get; } = created;

    public bool Exited { get; set; }

    /// <summary>Terminate fails with this Win32 error (0 = works).</summary>
    public int TerminateError { get; set; }

    /// <summary>Whether a graceful close (WM_CLOSE) ends it.</summary>
    public bool ClosesGracefully { get; set; }

    public int Terminations { get; set; }
}

/// <summary>Handles on <see cref="FakeProcess"/>es.</summary>
internal sealed class FakeHandle(FakeProcess process) : IProcessHandle
{
    public int Pid => process.Pid;

    public bool HasExited => process.Exited;

    public bool Disposed { get; private set; }

    public int Terminate()
    {
        process.Terminations++;
        if (process.TerminateError != 0)
        {
            return process.TerminateError;
        }

        process.Exited = true;
        return 0;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A closer over a fixed kill set of fake processes.</summary>
internal sealed class FakeCloser : IBlockedAppCloser
{
    private readonly Lock _lock = new();

    public List<FakeProcess> KillSet { get; } = [];

    public List<FakeHandle> Handles { get; } = [];

    public List<IReadOnlyList<ObservedProcess>> CloseRequests { get; } = [];

    public List<IReadOnlySet<(int, long)>> ExtraRootsSeen { get; } = [];

    public bool AgentAnswers { get; set; } = true;

    public bool CancelCloseRequests { get; set; }

    public Task<CloseAnswer?> RequestGracefulCloseAsync(int sessionId, IReadOnlyList<ObservedProcess> targets, CancellationToken ct)
    {
        lock (_lock)
        {
            CloseRequests.Add(targets);
            if (CancelCloseRequests)
            {
                return Task.FromCanceled<CloseAnswer?>(new CancellationToken(true));
            }

            if (!AgentAnswers)
            {
                return Task.FromResult<CloseAnswer?>(null);
            }

            foreach (var process in KillSet.Where(p => p.ClosesGracefully))
            {
                process.Exited = true;
            }

            return Task.FromResult<CloseAnswer?>(new CloseAnswer(1, targets.Count, 0));
        }
    }

    public IReadOnlyList<IProcessHandle> OpenKillSet(BlockedStart start, IReadOnlySet<(int Pid, long Created)> extraRoots)
    {
        lock (_lock)
        {
            ExtraRootsSeen.Add(extraRoots);
            var handles = KillSet.Where(p => !p.Exited).Select(p => new FakeHandle(p)).ToList();
            Handles.AddRange(handles);
            return handles;
        }
    }

    public IProcessHandle? Open(ObservedProcess process)
    {
        lock (_lock)
        {
            var match = KillSet.FirstOrDefault(p => p.Pid == process.Pid && p.Created == process.Created && !p.Exited);
            if (match is null)
            {
                return null;
            }

            var handle = new FakeHandle(match);
            Handles.Add(handle);
            return handle;
        }
    }
}
