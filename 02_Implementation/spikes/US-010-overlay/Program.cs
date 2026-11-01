namespace OverlaySpike;

/// <summary>Entry point of the US-010 overlay spike (throwaway test equipment).</summary>
internal static class Program
{
    private const string MutexName = @"Local\EagleEyeOverlaySpike";
    private static SpikeController? controller;
    private static SpikeLog? log;

    /// <summary>Logs an exception from a callback and shuts down (one balloon tip).</summary>
    public static void ReportError(Exception ex)
    {
        if (controller is not null)
        {
            controller.OnUnhandled(ex);
        }
        else
        {
            log?.Write("ERROR", ex.ToString().ReplaceLineEndings(" | "));
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        using var spikeLog = SpikeLog.Open(exeDir);
        log = spikeLog;
        if (!createdNew)
        {
            spikeLog.Write("START", "another OverlaySpike is already running in this session - exiting silently");
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => spikeLog.Write("ERROR", $"unhandled (terminating={e.IsTerminating}): {e.ExceptionObject}".ReplaceLineEndings(" | "));

        var notes = new List<string>();
        var settings = SpikeSettings.Load(exeDir, args, notes);
        try
        {
            using var context = new SpikeController(spikeLog, settings, notes);
            controller = context;
            Application.Run(context);
            controller = null;
        }
        catch (Exception ex)
        {
            spikeLog.Write("ERROR", "fatal: " + ex.ToString().ReplaceLineEndings(" | "));
            return 1;
        }

        GC.KeepAlive(mutex);
        return 0;
    }
}
