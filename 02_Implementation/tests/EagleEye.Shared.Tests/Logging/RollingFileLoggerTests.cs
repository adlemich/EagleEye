using EagleEye.Shared.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Shared.Tests.Logging;

public sealed class RollingFileLoggerTests : IDisposable
{
    private const string Prefix = "EagleEye.Service";
    private const string Category = "EagleEye.Service.UserAccounts.UserAccountService";

    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 17, 42, 7, 123, TimeSpan.Zero));
    private readonly RollingFileLoggerProvider _provider;

    public RollingFileLoggerTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _provider = new RollingFileLoggerProvider(new RollingFileOptions { Directory = _dir.Path, FilePrefix = Prefix }, _time);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _dir.Dispose();
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void IsEnabled_AnyRealLevel_ReturnsTrue(LogLevel level)
    {
        Assert.True(_provider.CreateLogger(Category).IsEnabled(level));
    }

    [Fact]
    public void IsEnabled_None_ReturnsFalse()
    {
        Assert.False(_provider.CreateLogger(Category).IsEnabled(LogLevel.None));
    }

    [Fact]
    public void Log_Information_WritesFormattedLineWithLocalTime()
    {
        var logger = _provider.CreateLogger(Category);

        logger.LogInformation("Account {UserName}: under parental control = {UnderParentalControl}.", "max", "yes");

        Assert.Equal(
            $"2026-10-08 17:42:07.123 +00:00 [INF] {Category}: Account max: under parental control = yes.{Environment.NewLine}",
            ReadLog());
    }

    [Fact]
    public void Log_WithException_WritesException()
    {
        var logger = _provider.CreateLogger(Category);
        var exception = new InvalidOperationException("boom");

        logger.LogWarning(exception, "Reading failed.");

        Assert.Contains(exception.ToString(), ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void Log_LevelNone_WritesNothing()
    {
        var logger = _provider.CreateLogger(Category);

        logger.Log(LogLevel.None, "never");

        Assert.False(File.Exists(LogPath));
    }

    [Fact]
    public void Log_EmptyMessageWithoutException_WritesNothing()
    {
        var logger = _provider.CreateLogger(Category);

        logger.Log(LogLevel.Information, default, "state", null, (_, _) => string.Empty);

        Assert.False(File.Exists(LogPath));
    }

    [Fact]
    public void Log_NullMessageWithException_WritesTheException()
    {
        var logger = _provider.CreateLogger(Category);
        var exception = new IOException("disk");

        logger.Log(LogLevel.Error, default, "state", exception, (_, _) => null!);

        Assert.Contains($"[ERR] {Category}: {Environment.NewLine}{exception}", ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void Log_NullFormatter_ThrowsArgumentNullException()
    {
        var logger = _provider.CreateLogger(Category);

        Assert.Throws<ArgumentNullException>(() => logger.Log<string>(LogLevel.Information, default, "state", null, null!));
    }

    [Fact]
    public void BeginScope_ReturnsDisposableNoOp()
    {
        var logger = _provider.CreateLogger(Category);

        using var scope = logger.BeginScope("scope");
        logger.LogInformation("inside");

        Assert.Equal((true, false), (scope is not null, ReadLog().Contains("scope", StringComparison.Ordinal)));
    }

    [Fact]
    public void CreateLogger_SameCategory_ReturnsSameInstance()
    {
        Assert.Same(_provider.CreateLogger(Category), _provider.CreateLogger(Category));
    }

    [Fact]
    public void CreateLogger_TwoCategories_WriteToTheSameFile()
    {
        _provider.CreateLogger("A").LogInformation("one");
        _provider.CreateLogger("B").LogInformation("two");

        Assert.Equal(2, ReadLog().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void CreateLogger_NullCategory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _provider.CreateLogger(null!));
    }

    [Fact]
    public void Dispose_ClosesTheFile()
    {
        _provider.CreateLogger(Category).LogInformation("x");

        _provider.Dispose();

        using var exclusive = new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    private string LogPath => _dir.File(Prefix + "-001.log");

    private string ReadLog()
    {
        using var reader = new StreamReader(new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        return reader.ReadToEnd();
    }
}
