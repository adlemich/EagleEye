using EagleEye.Service.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageHistoryLogTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";

    private readonly FakeTimeProvider _time = new();
    private readonly TestLogger<UsageService> _logger = new();
    private readonly UsageHistoryLog _log;

    public UsageHistoryLogTests()
    {
        _log = new UsageHistoryLog(_logger, new InstanceLogLimiter(_time));
    }

    [Fact]
    public void Entries_UseTheTemplates()
    {
        _log.NewApp("kid1", "Editor", "notepad.exe", @"C:\Windows\notepad.exe");
        _log.Started(Kid, "kid1", "Editor", "notepad.exe", @"C:\Windows\notepad.exe", 12);
        _log.Ended(Kid, "kid1", "Editor", "notepad.exe", @"C:\Windows\notepad.exe", 12, TimeSpan.FromSeconds(603), "closed");

        Assert.Equal(
            [
                @"New app for account kid1: Editor (notepad.exe, C:\Windows\notepad.exe).",
                @"App started: account kid1, Editor (notepad.exe, C:\Windows\notepad.exe), instance 12.",
                @"App ended: account kid1, Editor (notepad.exe, C:\Windows\notepad.exe), instance 12, duration 00:10:03 (closed).",
            ],
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public void StartedAndEnded_Limited_ThenSummary()
    {
        for (var i = 0; i < 20; i++)
        {
            _log.Started(Kid, "kid1", "Editor", "notepad.exe", @"C:\Windows\notepad.exe", i);
            _log.Ended(Kid, "kid1", "Editor", "notepad.exe", @"C:\Windows\notepad.exe", i, TimeSpan.Zero, "closed");
        }

        _time.Advance(InstanceLogLimiter.Window);
        _log.Flush();

        var messages = _logger.Messages(LogLevel.Information);
        Assert.Equal(31, messages.Count);
        Assert.Equal(@"10 further start/end entries of account kid1, Editor (C:\Windows\notepad.exe) were not logged in the last hour.", messages[^1]);
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(603, "00:10:03")]
    [InlineData(90_061, "25:01:01")]
    [InlineData(-5, "00:00:00")]
    public void FormatDuration(int seconds, string expected)
    {
        Assert.Equal(expected, UsageHistoryLog.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }
}
