using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class EnforcementHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IBlockedStartRepository> _blocked = new();
    private readonly Mock<ITimeChangeFindingRepository> _findings = new();
    private readonly TestLogger<EnforcementHistory> _logger = new();
    private readonly EnforcementHistory _history;

    public EnforcementHistoryTests()
    {
        _history = new EnforcementHistory(_blocked.Object, _findings.Object, new FakeTimeProvider(Now), _logger);
    }

    [Fact]
    public async Task InitializeAsync_CompletesDanglingAndPurges()
    {
        _blocked.Setup(b => b.CompleteDanglingAsync(BlockedStartTexts.UnknownServiceStopped, Now, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await _history.InitializeAsync();

        Assert.Contains("2 blocked start(s) left open by a service stop were completed as \"unknown (service stopped)\".", _logger.Messages(LogLevel.Information));
        _blocked.Verify(b => b.PurgeOlderThanAsync(Now.AddDays(-90), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_NothingDanglingOrOld_NoLog()
    {
        await _history.InitializeAsync();

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task PurgeOldDataAsync_BothTables90DaysAndLog()
    {
        _blocked.Setup(b => b.PurgeOlderThanAsync(Now.AddDays(-90), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _findings.Setup(f => f.PurgeOlderThanAsync(Now.AddDays(-90), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _history.PurgeOldDataAsync();

        Assert.Equal("Purged enforcement records older than 90 days: 3 blocked starts, 1 time-change findings.", _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public async Task PurgeMissingAccountsAsync_BothTablesAndLog()
    {
        string[] existing = ["S-1"];
        _findings.Setup(f => f.PurgeAccountsNotInAsync(existing, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await _history.PurgeMissingAccountsAsync(existing);

        _blocked.Verify(b => b.PurgeAccountsNotInAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Purged enforcement records of deleted accounts: 0 blocked starts, 2 time-change findings.", _logger.Messages(LogLevel.Information).Single());
    }

    [Fact]
    public async Task PurgeMissingAccountsAsync_NothingPurged_NoLog()
    {
        await _history.PurgeMissingAccountsAsync([]);

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task PurgeMissingAccountsAsync_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _history.PurgeMissingAccountsAsync(null!));
    }
}
