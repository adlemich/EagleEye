using EagleEye.Shared.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EagleEye.Shared.Tests.Logging;

public sealed class LogLineFormatterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 8, 19, 42, 7, 123, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(LogLevel.Trace, "TRC")]
    [InlineData(LogLevel.Debug, "DBG")]
    [InlineData(LogLevel.Information, "INF")]
    [InlineData(LogLevel.Warning, "WRN")]
    [InlineData(LogLevel.Error, "ERR")]
    [InlineData(LogLevel.Critical, "CRT")]
    [InlineData(LogLevel.None, "???")]
    [InlineData((LogLevel)42, "???")]
    public void GetLevelCode_ReturnsThreeLetterCode(LogLevel level, string expected)
    {
        Assert.Equal(expected, LogLineFormatter.GetLevelCode(level));
    }

    [Fact]
    public void Format_WithoutException_IsOneLineWithTimestampLevelCategoryAndMessage()
    {
        var line = LogLineFormatter.Format(Timestamp, LogLevel.Information, "EagleEye.Service.X", "Hello", exception: null);

        Assert.Equal("2026-10-08 19:42:07.123 +02:00 [INF] EagleEye.Service.X: Hello" + Environment.NewLine, line);
    }

    [Fact]
    public void Format_WithException_AppendsExceptionOnFollowingLines()
    {
        var exception = new InvalidOperationException("boom");

        var line = LogLineFormatter.Format(Timestamp, LogLevel.Error, "Cat", "Failed", exception);

        Assert.Equal(
            "2026-10-08 19:42:07.123 +02:00 [ERR] Cat: Failed" + Environment.NewLine + exception + Environment.NewLine,
            line);
    }

    [Fact]
    public void Format_NegativeOffset_IsWrittenWithSign()
    {
        var line = LogLineFormatter.Format(Timestamp.ToOffset(TimeSpan.FromHours(-5)), LogLevel.Warning, "C", "m", null);

        Assert.StartsWith("2026-10-08 12:42:07.123 -05:00 [WRN]", line, StringComparison.Ordinal);
    }
}
