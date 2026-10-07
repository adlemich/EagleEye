using System.Globalization;
using System.Text;

namespace EagleEye.Shared.Logging;

/// <summary>
/// Appends text to <c>{prefix}-NNN.log</c> files (number <c>D3</c>, grows beyond 999). On the first
/// write it continues the highest existing file if it is below the size limit, otherwise it starts
/// the next number. Before a write that would exceed the limit it starts the next file. Whenever it
/// opens a file it cleans up: it keeps the newest <see cref="RollingFileOptions.MaxFiles"/> files and
/// deletes non-current files older than <see cref="RollingFileOptions.MaxAge"/>. UTF-8 without BOM,
/// readable while open, flushed after every entry. Thread-safe. The folder is never created here, so
/// files never land in a folder without the owner's ACL. I/O errors are swallowed: logging must never
/// stop the application (coding guidelines §9.1).
/// </summary>
internal sealed class RollingFileWriter : IDisposable
{
    private const string Extension = ".log";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly RollingFileOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private FileStream? _stream;
    private int _currentNumber;

    /// <summary>Creates the writer; no file is opened before the first write.</summary>
    public RollingFileWriter(RollingFileOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FilePrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxFiles);
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>The full path of a log file number.</summary>
    public string GetFilePath(int number)
    {
        return Path.Combine(
            _options.Directory,
            _options.FilePrefix + "-" + number.ToString("D3", CultureInfo.InvariantCulture) + Extension);
    }

    /// <summary>Appends the text. Never throws for I/O errors.</summary>
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Utf8NoBom.GetBytes(text);
        lock (_lock)
        {
            try
            {
                var stream = _stream ?? OpenInitial();
                if (stream.Length > 0 && stream.Length + bytes.Length > _options.MaxFileBytes)
                {
                    stream = OpenFile(_currentNumber + 1);
                }

                stream.Write(bytes);
                stream.Flush();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never stop the application. The file is opened again on the next write.
                CloseStream();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            CloseStream();
        }
    }

    private FileStream OpenInitial()
    {
        var existing = FindLogFiles();
        if (existing.Count == 0)
        {
            return OpenFile(1);
        }

        var (number, path) = existing[0];
        return OpenFile(new FileInfo(path).Length < _options.MaxFileBytes ? number : number + 1);
    }

    private FileStream OpenFile(int number)
    {
        CloseStream();
        var stream = new FileStream(
            GetFilePath(number), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _stream = stream;
        _currentNumber = number;
        CleanUp();
        return stream;
    }

    private void CleanUp()
    {
        var oldest = _timeProvider.GetUtcNow().UtcDateTime - _options.MaxAge;
        var files = FindLogFiles();
        for (var index = 0; index < files.Count; index++)
        {
            var (number, path) = files[index];
            if (number != _currentNumber && (index >= _options.MaxFiles || File.GetLastWriteTimeUtc(path) < oldest))
            {
                TryDelete(path);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file that is open elsewhere is deleted at the next clean-up; writing goes on.
        }
    }

    /// <summary>Existing log files of this prefix, highest number first.</summary>
    private List<(int Number, string Path)> FindLogFiles()
    {
        var files = new List<(int Number, string Path)>();
        foreach (var path in Directory.EnumerateFiles(_options.Directory, _options.FilePrefix + "-*" + Extension))
        {
            var digits = Path.GetFileNameWithoutExtension(path)[(_options.FilePrefix.Length + 1)..];
            if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
            {
                files.Add((number, path));
            }
        }

        files.Sort((a, b) => b.Number.CompareTo(a.Number));
        return files;
    }

    private void CloseStream()
    {
        _stream?.Dispose();
        _stream = null;
    }
}
