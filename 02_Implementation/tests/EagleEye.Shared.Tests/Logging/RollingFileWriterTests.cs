using System.Text;
using EagleEye.Shared.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Shared.Tests.Logging;

public sealed class RollingFileWriterTests : IDisposable
{
    private const string Prefix = "EagleEye.Service";
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(Now);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Write_EmptyFolder_CreatesFile001WithText()
    {
        using var writer = CreateWriter();

        writer.Write("hello\n");

        Assert.Equal("hello\n", ReadFile(1));
    }

    [Fact]
    public void Write_Utf8WithoutBom()
    {
        using var writer = CreateWriter();

        writer.Write("Ärger\n");

        Assert.Equal(Encoding.UTF8.GetBytes("Ärger\n"), ReadBytes(LogPath(1)));
    }

    [Fact]
    public void Write_HighestExistingFileBelowLimit_ContinuesIt()
    {
        CreateLogFile(1, "one\n");
        CreateLogFile(2, "two\n");
        using var writer = CreateWriter();

        writer.Write("more\n");

        Assert.Equal(("two\nmore\n", false), (ReadFile(2), File.Exists(LogPath(3))));
    }

    [Fact]
    public void Write_HighestExistingFileAtLimit_StartsNextNumber()
    {
        CreateLogFile(2, new string('x', 100));
        using var writer = CreateWriter(maxFileBytes: 100);

        writer.Write("next\n");

        Assert.Equal("next\n", ReadFile(3));
    }

    [Fact]
    public void Write_WouldExceedLimit_RollsOverToNextFile()
    {
        using var writer = CreateWriter(maxFileBytes: 100);
        var entry = new string('a', 59) + "\n";

        writer.Write(entry);
        writer.Write(entry);

        Assert.Equal((entry, entry), (ReadFile(1), ReadFile(2)));
    }

    [Fact]
    public void Write_ExactlyAtLimit_StaysInFile()
    {
        using var writer = CreateWriter(maxFileBytes: 100);

        writer.Write(new string('a', 50));
        writer.Write(new string('b', 50));

        Assert.Equal((100, false), (ReadFile(1).Length, File.Exists(LogPath(2))));
    }

    [Fact]
    public void Write_EntryLargerThanLimitIntoEmptyFile_WritesItThere()
    {
        using var writer = CreateWriter(maxFileBytes: 10);

        writer.Write(new string('a', 20));

        Assert.Equal((20, false), (ReadFile(1).Length, File.Exists(LogPath(2))));
    }

    [Fact]
    public void Write_NumberAbove999_GrowsBeyondThreeDigits()
    {
        CreateLogFile(999, new string('x', 10));
        using var writer = CreateWriter(maxFileBytes: 10);

        writer.Write("x");

        Assert.True(File.Exists(_dir.File(Prefix + "-1000.log")));
    }

    [Fact]
    public void Write_MoreThanMaxFiles_KeepsNewestThree()
    {
        for (var number = 1; number <= 4; number++)
        {
            CreateLogFile(number, "old\n");
        }

        using var writer = CreateWriter();

        writer.Write("x");

        Assert.Equal([false, true, true, true], Enumerable.Range(1, 4).Select(n => File.Exists(LogPath(n))));
    }

    [Fact]
    public void Write_StartsNextFile_DeletesBeyondMaxFiles()
    {
        CreateLogFile(1, "1");
        CreateLogFile(2, "2");
        CreateLogFile(3, new string('3', 10));
        using var writer = CreateWriter(maxFileBytes: 10);

        writer.Write("4");

        Assert.Equal([false, true, true, true], Enumerable.Range(1, 4).Select(n => File.Exists(LogPath(n))));
    }

    [Fact]
    public void Write_NonCurrentFileOlderThanFiveDays_IsDeleted()
    {
        CreateLogFile(1, "old\n", age: TimeSpan.FromDays(5) + TimeSpan.FromSeconds(1));
        CreateLogFile(2, "recent\n", age: TimeSpan.FromDays(1));
        using var writer = CreateWriter();

        writer.Write("x");

        Assert.Equal((false, true), (File.Exists(LogPath(1)), File.Exists(LogPath(2))));
    }

    [Fact]
    public void Write_NonCurrentFileExactlyFiveDaysOld_IsKept()
    {
        CreateLogFile(1, "old\n", age: TimeSpan.FromDays(5));
        CreateLogFile(2, "recent\n");
        using var writer = CreateWriter();

        writer.Write("x");

        Assert.True(File.Exists(LogPath(1)));
    }

    [Fact]
    public void Write_CurrentFileOlderThanFiveDays_IsNeverDeleted()
    {
        CreateLogFile(1, "old\n", age: TimeSpan.FromDays(30));
        using var writer = CreateWriter();

        writer.Write("x");

        Assert.Equal("old\nx", ReadFile(1));
    }

    [Fact]
    public void Write_OldFileLockedElsewhere_KeepsWriting()
    {
        CreateLogFile(1, "old\n", age: TimeSpan.FromDays(30));
        CreateLogFile(2, "recent\n");
        using var locked = new FileStream(LogPath(1), FileMode.Open, FileAccess.Read, FileShare.None);
        using var writer = CreateWriter();

        writer.Write("x");

        Assert.Equal("recent\nx", ReadFile(2));
    }

    [Fact]
    public void Write_UnrelatedFiles_AreIgnoredAndKept()
    {
        string[] unrelated = [Prefix + "-abc.log", Prefix + "-000.log", Prefix + "--01.log", "Other-001.log", Prefix + "-001.txt"];
        foreach (var name in unrelated)
        {
            File.WriteAllText(_dir.File(name), "keep");
        }

        using var writer = CreateWriter(maxFiles: 1);

        writer.Write("x");

        Assert.Equal(("x", true), (ReadFile(1), unrelated.All(name => File.Exists(_dir.File(name)))));
    }

    [Fact]
    public void Write_FolderMissing_IsSwallowedAndFolderNotCreated()
    {
        var missing = Path.Combine(_dir.Path, "missing");
        using var writer = new RollingFileWriter(new RollingFileOptions { Directory = missing, FilePrefix = Prefix }, _time);

        writer.Write("x");

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void Write_AfterFailure_OpensTheFileAgain()
    {
        var folder = Path.Combine(_dir.Path, "later");
        using var writer = new RollingFileWriter(new RollingFileOptions { Directory = folder, FilePrefix = Prefix }, _time);
        writer.Write("lost");
        Directory.CreateDirectory(folder);

        writer.Write("kept");

        Assert.Equal("kept", Encoding.UTF8.GetString(ReadBytes(Path.Combine(folder, Prefix + "-001.log"))));
    }

    [Fact]
    public void Write_AccessDenied_IsSwallowed()
    {
        // A folder with the file's name makes opening the file fail with UnauthorizedAccessException.
        Directory.CreateDirectory(LogPath(1));
        using var writer = CreateWriter();

        var exception = Record.Exception(() => writer.Write("x"));

        Assert.Null(exception);
    }

    [Fact]
    public void Write_FileIsReadableWhileOpen()
    {
        using var writer = CreateWriter();
        writer.Write("line\n");

        using var reader = new StreamReader(new FileStream(LogPath(1), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));

        Assert.Equal("line\n", reader.ReadToEnd());
    }

    [Fact]
    public void Write_Concurrent_LinesDoNotInterleave()
    {
        using var writer = CreateWriter();
        var expected = Enumerable.Range(0, 200).Select(i => $"entry {i:D3} " + new string('z', 200)).ToArray();

        Parallel.ForEach(expected, line => writer.Write(line + "\n"));

        Assert.Equal(expected.Order(), ReadFile(1).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order());
    }

    [Fact]
    public void Dispose_ClosesFile()
    {
        var writer = CreateWriter();
        writer.Write("x");

        writer.Dispose();
        writer.Dispose();

        using var exclusive = new FileStream(LogPath(1), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(1, exclusive.Length);
    }

    [Fact]
    public void GetFilePath_FormatsNumberWithThreeDigits()
    {
        using var writer = CreateWriter();

        Assert.Equal(_dir.File("EagleEye.Service-007.log"), writer.GetFilePath(7));
    }

    [Fact]
    public void Write_NullText_ThrowsArgumentNullException()
    {
        using var writer = CreateWriter();

        Assert.Throws<ArgumentNullException>(() => writer.Write(null!));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RollingFileWriter(null!, _time));
    }

    [Fact]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RollingFileWriter(Options(), null!));
    }

    [Theory]
    [InlineData("", Prefix, 1L, 1)]
    [InlineData(" ", Prefix, 1L, 1)]
    [InlineData("dir", "", 1L, 1)]
    [InlineData("dir", Prefix, 0L, 1)]
    [InlineData("dir", Prefix, 1L, 0)]
    public void Constructor_InvalidOptions_ThrowsArgumentException(string directory, string prefix, long maxBytes, int maxFiles)
    {
        var options = new RollingFileOptions { Directory = directory, FilePrefix = prefix, MaxFileBytes = maxBytes, MaxFiles = maxFiles };

        Assert.ThrowsAny<ArgumentException>(() => new RollingFileWriter(options, _time));
    }

    [Fact]
    public void Options_Defaults_Are50MegabytesThreeFilesFiveDays()
    {
        var options = Options();

        Assert.Equal((50L * 1024 * 1024, 3, TimeSpan.FromDays(5)), (options.MaxFileBytes, options.MaxFiles, options.MaxAge));
    }

    private RollingFileOptions Options(long maxFileBytes = RollingFileOptions.DefaultMaxFileBytes, int maxFiles = 3)
    {
        return new RollingFileOptions { Directory = _dir.Path, FilePrefix = Prefix, MaxFileBytes = maxFileBytes, MaxFiles = maxFiles };
    }

    private RollingFileWriter CreateWriter(long maxFileBytes = RollingFileOptions.DefaultMaxFileBytes, int maxFiles = 3)
    {
        return new RollingFileWriter(Options(maxFileBytes, maxFiles), _time);
    }

    private string LogPath(int number) => _dir.File($"{Prefix}-{number:D3}.log");

    private static byte[] ReadBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private string ReadFile(int number)
    {
        using var reader = new StreamReader(new FileStream(LogPath(number), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        return reader.ReadToEnd();
    }

    private void CreateLogFile(int number, string content, TimeSpan? age = null)
    {
        File.WriteAllText(LogPath(number), content);
        File.SetLastWriteTimeUtc(LogPath(number), (Now - (age ?? TimeSpan.FromHours(1))).UtcDateTime);
    }
}
