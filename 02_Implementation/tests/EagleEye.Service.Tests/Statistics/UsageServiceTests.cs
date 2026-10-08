using EagleEye.Service.Data;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Statistics;

public sealed class UsageServiceTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\notepad.exe";

    private static readonly DateTimeOffset Start = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) { [Kid] = "kid1", [Kid2] = "kid2" };

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly UsageRepository _repository;
    private readonly List<DayUsageDto> _broadcasts = [];
    private readonly Mock<IUsageBroadcaster> _broadcaster = new();
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly TestLogger<UsageService> _logger = new();
    private UsageService _service;

    public UsageServiceTests()
    {
        _time.SetLocalTimeZone(TestZones.Berlin);
        _repository = new UsageRepository(_database);
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<DayUsageDto>())).Callback<DayUsageDto>(_broadcasts.Add).Returns(Task.CompletedTask);
        _accounts.Setup(a => a.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserAccountListDto(1, null,
            [new UserAccountDto(Kid, "kid1", null, false, true), new UserAccountDto(Kid2, "kid2", null, false, false)]));
        _service = Create(_repository);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Initialize_ClosesDanglingInstancesAtLastSeen()
    {
        var app = await _repository.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Start);
        var instance = await _repository.StartInstanceAsync(app.AppId, Start);
        await _repository.ApplyAsync([], [new InstanceSeenRecord(instance, Start.AddMinutes(10).AddSeconds(3))]);

        await _service.InitializeAsync();

        Assert.Empty(await _repository.GetOpenInstancesAsync());
        Assert.Contains(
            $@"App ended: account kid1, Editor (notepad.exe, {Notepad}), instance {instance}, duration 00:10:03 (service stopped unexpectedly).",
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task Initialize_InventoryUnavailable_LogsSid()
    {
        _accounts.Setup(a => a.GetSnapshotAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new AccountInventoryUnavailableException());
        var app = await _repository.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Start);
        await _repository.StartInstanceAsync(app.AppId, Start);

        await _service.InitializeAsync();

        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.StartsWith($"App ended: account {Kid},", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Initialize_PurgesOldDataAndLogs()
    {
        var app = await _repository.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Start);
        await _repository.ApplyAsync([new UsageIncrement(app.AppId, Today.AddDays(-90), 5)], []);

        await _service.InitializeAsync();

        Assert.Contains("Purged usage data older than 2026-07-10: 1 daily entries, 0 history entries.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task PurgeOldData_AfterMidnight_DeletesDay90()
    {
        var app = await _repository.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Start);
        await _repository.ApplyAsync([new UsageIncrement(app.AppId, Today.AddDays(-89), 5)], []);
        _time.Advance(TimeSpan.FromDays(1));

        await _service.PurgeOldDataAsync();

        Assert.Empty(await _repository.GetUsageAsync(Kid, Today.AddDays(-100), Today));
    }

    [Fact]
    public async Task Initialize_NothingToPurge_NoLog()
    {
        await _service.InitializeAsync();

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task Start_CreatesRecordLogsAndShowsZeroAfterTick()
    {
        await ApplyAsync(Started(1), isTick: true);

        var info = _logger.Messages(LogLevel.Information);
        Assert.Equal($@"New app for account kid1: Editor (notepad.exe, {Notepad}).", info[0]);
        Assert.StartsWith("App started: account kid1, Editor", info[1], StringComparison.Ordinal);
        var broadcast = Assert.Single(_broadcasts);
        Assert.Equal((2L, (Guid?)null, Kid, Today, Today, 0L), (broadcast.Revision, broadcast.LastChangeRequestId, broadcast.AccountSid, broadcast.Day, broadcast.ServiceToday, broadcast.Apps.Single().Seconds));
    }

    [Fact]
    public async Task Start_ExistingApp_NoNewAppEntry()
    {
        await ApplyAsync(Started(1));
        await ApplyAsync(Ended(1));
        await ApplyAsync(Started(2));

        Assert.Single(_logger.Messages(LogLevel.Information), m => m.StartsWith("New app", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_UnknownUserName_UsesSid()
    {
        await _service.ApplyAsync(new TrackerOutput([Started(1)], [], []), new Dictionary<string, string>(), isTick: false);

        Assert.StartsWith($"New app for account {Kid}:", _logger.Messages(LogLevel.Information)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tick_CreditsPersistedAndBroadcastOncePerDayWithIncreasingRevision()
    {
        await ApplyAsync(Started(1), isTick: true);
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5), Credit(3), Credit(2, Today.AddDays(-1))], []), Names, isTick: true);

        var usage = await _service.GetAccountUsageAsync(Kid);

        Assert.Equal([(2L, Today, 0L), (3L, Today, 8L), (4L, Today.AddDays(-1), 2L)], _broadcasts.Select(b => (b.Revision, b.Day, b.Apps.Single().Seconds)));
        Assert.Equal([8L, 2L], usage.Days.Select(d => d.Apps.Single().Seconds));
        Assert.All(usage.Days, d => Assert.Equal(4L, d.Revision));
    }

    [Fact]
    public async Task Tick_NothingChanged_NoBroadcast()
    {
        await ApplyAsync(Started(1), isTick: true);
        _broadcasts.Clear();

        await _service.ApplyAsync(TrackerOutput.Empty, Names, isTick: true);

        Assert.Empty(_broadcasts);
    }

    [Fact]
    public async Task NonTick_PersistsOnlyAtNextTick()
    {
        await ApplyAsync(Started(1), isTick: true);
        _broadcasts.Clear();
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5)], []), Names, isTick: false);
        var before = _broadcasts.Count;

        await _service.ApplyAsync(TrackerOutput.Empty, Names, isTick: true);

        Assert.Equal((0, 5L), (before, _broadcasts.Single().Apps.Single().Seconds));
    }

    [Fact]
    public async Task Credit_UnknownApp_Ignored()
    {
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5)], [new InstanceSeen(99, Start)]), Names, isTick: true);

        Assert.Empty(_broadcasts);
    }

    [Fact]
    public async Task Seen_UpdatesLastSeenOfInstance()
    {
        await ApplyAsync(Started(1));

        await _service.ApplyAsync(new TrackerOutput([], [], [new InstanceSeen(1, Start.AddSeconds(30))]), Names, isTick: true);

        Assert.Equal(Start.AddSeconds(30), (await _repository.GetOpenInstancesAsync()).Single().LastSeenUtc);
    }

    [Fact]
    public async Task End_StoresAndLogsDuration()
    {
        await ApplyAsync(Started(1));
        await _service.ApplyAsync(new TrackerOutput([], [], [new InstanceSeen(1, Start.AddSeconds(30))]), Names, isTick: false);

        await ApplyAsync(Ended(1, seconds: 603));
        await ApplyAsync(Ended(1));
        await _service.ApplyAsync(TrackerOutput.Empty, Names, isTick: true);

        Assert.Empty(await _repository.GetOpenInstancesAsync());
        Assert.Single(_logger.Messages(LogLevel.Information), m => m.EndsWith("duration 00:10:03 (closed).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BroadcastFails_WarningAndRecordingContinues()
    {
        var failure = new IOException("hub");
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<DayUsageDto>())).ThrowsAsync(failure);

        await ApplyAsync(Started(1), isTick: true);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Single((await _service.GetAccountUsageAsync(Kid)).Days[0].Apps);
    }

    [Fact]
    public async Task PersistFails_KeptAndRetriedWithNextTick()
    {
        var repository = new Mock<IUsageRepository>();
        repository.Setup(r => r.GetOrCreateAppAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppRecordResult(7, true));
        repository.Setup(r => r.GetUsageAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var applied = new List<IReadOnlyCollection<UsageIncrement>>();
        var failure = new IOException("disk full");
        repository.SetupSequence(r => r.ApplyAsync(It.IsAny<IReadOnlyCollection<UsageIncrement>>(), It.IsAny<IReadOnlyCollection<InstanceSeenRecord>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.ApplyAsync(It.IsAny<IReadOnlyCollection<UsageIncrement>>(), It.IsAny<IReadOnlyCollection<InstanceSeenRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<UsageIncrement>, IReadOnlyCollection<InstanceSeenRecord>, CancellationToken>((i, _, _) => applied.Add([.. i]))
            .Returns(() => applied.Count == 1 ? Task.FromException(failure) : Task.CompletedTask);
        _service = Create(repository.Object);

        await ApplyAsync(Started(1));
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5)], []), Names, isTick: true);
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5)], []), Names, isTick: true);

        Assert.True(_logger.Has(LogLevel.Warning, failure));
        Assert.Equal(10, applied[1].Single().Seconds);
        Assert.Single(_broadcasts);
    }

    [Fact]
    public async Task GetAccountUsage_UnknownAccount_Throws()
    {
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.GetAccountUsageAsync("S-1-5-21-9"));
    }

    [Fact]
    public async Task GetAccountUsage_NoData_TodayEmptyRevision1()
    {
        var usage = await _service.GetAccountUsageAsync(Kid.ToLowerInvariant());

        Assert.Equal((Today, 1, 1L, 0), (usage.ServiceToday, usage.Days.Count, usage.Days[0].Revision, usage.Days[0].Apps.Count));
    }

    [Fact]
    public async Task GetAccountUsage_TodayFirstThenNewestOlderWithin90Days()
    {
        var app = await _repository.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Start);
        await _repository.ApplyAsync(
            [new UsageIncrement(app.AppId, Today.AddDays(-3), 1), new UsageIncrement(app.AppId, Today.AddDays(-1), 2),
             new UsageIncrement(app.AppId, Today.AddDays(-89), 3), new UsageIncrement(app.AppId, Today.AddDays(-90), 4)],
            []);

        var usage = await _service.GetAccountUsageAsync(Kid);

        Assert.Equal([Today, Today.AddDays(-1), Today.AddDays(-3), Today.AddDays(-89)], usage.Days.Select(d => d.Day));
    }

    [Fact]
    public async Task PurgeMissingAccounts_LogsAndForgetsPendingUsage()
    {
        await ApplyAsync(Started(1));
        await _service.ApplyAsync(new TrackerOutput([], [Credit(5)], []), Names, isTick: false);

        await _service.PurgeMissingAccountsAsync([Kid2]);
        await _service.ApplyAsync(TrackerOutput.Empty, Names, isTick: true);

        Assert.Contains($"Purged all recorded data of deleted account {Kid}: 1 apps, 1 history entries, 0 daily entries.", _logger.Messages(LogLevel.Information));
        Assert.Empty(_broadcasts);
        Assert.Empty(_logger.Messages(LogLevel.Warning));
    }

    [Fact]
    public async Task PurgeMissingAccounts_OtherAccountKept()
    {
        await ApplyAsync(Started(1));
        await _service.ApplyAsync(new TrackerOutput([new InstanceStarted(2, Kid2, Notepad, "notepad.exe", "Editor", Start)], [], []), Names, isTick: false);

        await _service.PurgeMissingAccountsAsync([Kid]);
        await _service.ApplyAsync(TrackerOutput.Empty, Names, isTick: true);

        Assert.Equal(Kid, _broadcasts.Single().AccountSid);
    }

    [Fact]
    public async Task PublishToday_EmptySnapshotPerAccount()
    {
        await _service.PublishTodayAsync([Kid, Kid2]);

        Assert.Equal([(Kid, 2L, 0), (Kid2, 2L, 0)], _broadcasts.Select(b => (b.AccountSid, b.Revision, b.Apps.Count)));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ApplyAsync(null!, Names, true));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ApplyAsync(TrackerOutput.Empty, null!, true));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.GetAccountUsageAsync(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.PublishTodayAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.PurgeMissingAccountsAsync(null!));
    }

    private UsageService Create(IUsageRepository repository) => new(repository, _broadcaster.Object, _accounts.Object, _time, _logger);

    private Task ApplyAsync(InstanceEvent instance, bool isTick = false) =>
        _service.ApplyAsync(new TrackerOutput([instance], [], []), Names, isTick);

    private static InstanceStarted Started(long key) => new(key, Kid, Notepad, "notepad.exe", "Editor", Start);

    private static InstanceEnded Ended(long key, int seconds = 0) =>
        new(key, Kid, Notepad, "notepad.exe", "Editor", Start, Start.AddSeconds(seconds), EndReasons.Closed);

    private static UsageCredit Credit(long seconds, DateOnly? day = null) => new(Kid, Notepad.ToUpperInvariant(), day ?? Today, seconds);
}
