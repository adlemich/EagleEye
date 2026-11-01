using System.Text;

namespace OverlaySpike;

/// <summary>
/// Log file overlay-spike-&lt;yyyyMMdd-HHmmss&gt;.log next to the exe, or in %LOCALAPPDATA%\OverlaySpike\ when the exe
/// folder is not writable. One line per event, flushed after every line.
/// </summary>
internal sealed class SpikeLog : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;

    private SpikeLog(string folder, StreamWriter writer, bool fallback)
    {
        Folder = folder;
        this.writer = writer;
        IsFallback = fallback;
    }

    public string Folder { get; }

    public bool IsFallback { get; }

    public static SpikeLog Open(string exeDir)
    {
        var name = $"overlay-spike-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        if (TryCreate(exeDir, name, out var writer))
        {
            return new SpikeLog(exeDir, writer, false);
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OverlaySpike");
        Directory.CreateDirectory(fallback);
        if (!TryCreate(fallback, name, out writer))
        {
            name = $"overlay-spike-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
            writer = Create(fallback, name);
        }

        return new SpikeLog(fallback, writer, true);
    }

    public void Write(string category, string message)
    {
        lock (gate)
        {
            try
            {
                writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {category,-8}  {message}");
            }
            catch (IOException)
            {
                // Nothing sensible to do in a throwaway spike when the disk is gone.
            }
            catch (ObjectDisposedException)
            {
                // Late events after shutdown.
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            writer.Dispose();
        }
    }

    private static bool TryCreate(string folder, string name, out StreamWriter writer)
    {
        try
        {
            writer = Create(folder, name);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            writer = null!; // Only used when the method returns true.
            return false;
        }
    }

    private static StreamWriter Create(string folder, string name)
    {
        var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }
}
