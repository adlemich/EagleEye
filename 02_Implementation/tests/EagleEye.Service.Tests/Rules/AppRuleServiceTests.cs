using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Rules;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class AppRuleServiceTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Device = "Dad's laptop";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";
    private const string Explorer = @"C:\Windows\explorer.exe";
    private const string EnglishBudgetDefault = "You have used up your time! Sorry, that's it for now.";
    private static readonly Guid RequestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RequestB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AppRuleRepository _repository;
    private readonly UsageRepository _usage;
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly List<AppRulesDto> _broadcasts = [];
    private readonly Mock<IAccountRulesBroadcaster> _broadcaster = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly TestLogger<AppRuleService> _logger = new();
    private AppRuleService _service;
    private long _notepad;

    public AppRuleServiceTests()
    {
        _repository = new AppRuleRepository(_database);
        _usage = new UsageRepository(_database);
        _accounts.Setup(a => a.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserAccountListDto(1, null,
        [
            new UserAccountDto(Kid, "kid1", null, false, true),
            new UserAccountDto(Kid2, "kid2", null, false, false),
        ]));
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AppRulesDto>())).Callback<AppRulesDto>(_broadcasts.Add).Returns(Task.CompletedTask);
        _service = Create(_repository);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _notepad = (await _usage.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Now)).AppId;
        await _service.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    // ---------- Initialize / Get ----------

    [Fact]
    public async Task InitializeAsync_LoadsRecordsRulesAndTexts_IgnoredProgramsLeftOut()
    {
        var paint = (await _usage.GetOrCreateAppAsync(Kid, @"C:\Apps\paint.exe", "paint.exe", "Paint", Now)).AppId;
        await _usage.GetOrCreateAppAsync(Kid, Explorer, "explorer.exe", "Windows-Explorer", Now);
        await _repository.SetRuleAsync(Kid, paint, 30, BreakTimeDays.Saturday, Now);
        await _repository.SetBudgetTextAsync(Kid2, "Ende", Now);

        await _service.InitializeAsync();

        Assert.Equal(new AppRuleEntry(paint, @"C:\Apps\paint.exe", "Paint", 30, BreakTimeDays.Saturday), _service.Current.RuleFor(Kid, @"c:\apps\PAINT.exe"));
        Assert.Null(_service.Current.RuleFor(Kid, Explorer));
        Assert.Equal("Ende", _service.Current.For(Kid2).BudgetText);
        Assert.Contains("App rules loaded: 1 rules of 2 apps of 1 accounts, 1 changed budget texts.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task GetAsync_DefaultRuleAndDefaultText()
    {
        var dto = await _service.GetAsync(Kid.ToLowerInvariant());

        Assert.Equal((1L, (Guid?)null, Kid, (string?)null), (dto.Revision, dto.LastChangeRequestId, dto.AccountSid, dto.BudgetText));
        Assert.Equal([new AppRuleDto(_notepad, "Editor", Notepad, 0, BreakTimeDays.None)], dto.Apps);
    }

    [Fact]
    public async Task GetAsync_AccountWithoutRecords_Empty()
    {
        Assert.Empty((await _service.GetAsync(Kid2)).Apps);
    }

    [Fact]
    public async Task GetAsync_UnknownAccount_Throws()
    {
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.GetAsync("S-1-5-21-1-2-3-1999"));
    }

    // ---------- Budget ----------

    [Fact]
    public async Task SetBudgetAsync_HoursThenMinutes_CombinedWithTheStoredPart()
    {
        await _service.SetBudgetAsync(RequestA, Kid, _notepad, AppBudgetPart.Minutes, 45, Device);
        var ack = await _service.SetBudgetAsync(RequestB, Kid, _notepad, AppBudgetPart.Hours, 2, Device);

        Assert.Equal(3, ack.Revision);
        Assert.Equal(165, _service.Current.RuleFor(Kid, Notepad)!.BudgetMinutes);
        Assert.Equal(165, (await _repository.LoadAllAsync()).Apps.Single().BudgetMinutes);
        Assert.Equal((3L, RequestB, 165), (_broadcasts[^1].Revision, _broadcasts[^1].LastChangeRequestId, _broadcasts[^1].Apps.Single().BudgetMinutes));
        Assert.EndsWith(
            "App rules of account kid1 (S-1-5-21-1-2-3-1003) changed by Dad's laptop: app rule changed: Editor, 2:45, no day (request bbbbbbbb-0000-0000-0000-000000000002, revision 3).",
            _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AppBudgetPart.Hours, 24)]
    [InlineData(AppBudgetPart.Minutes, 60)]
    [InlineData(AppBudgetPart.Minutes, -1)]
    public async Task SetBudgetAsync_OutOfRange_Throws(AppBudgetPart part, int value)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SetBudgetAsync(RequestA, Kid, _notepad, part, value, Device));
    }

    [Fact]
    public async Task SetBudgetAsync_AppOfAnotherAccount_NotFound()
    {
        await Assert.ThrowsAsync<AppNotFoundException>(() => _service.SetBudgetAsync(RequestA, Kid2, _notepad, AppBudgetPart.Hours, 1, Device));
    }

    [Fact]
    public async Task SetBudgetAsync_RecordGoneFromTheDatabase_NotFoundAndSnapshotUnchanged()
    {
        await _usage.PurgeAccountsNotInAsync([Kid2]);

        await Assert.ThrowsAsync<AppNotFoundException>(() => _service.SetBudgetAsync(RequestA, Kid, _notepad, AppBudgetPart.Hours, 1, Device));
        Assert.Equal(0, _service.Current.RuleFor(Kid, Notepad)!.BudgetMinutes);
    }

    [Fact]
    public async Task SetBudgetAsync_NoOp_StillOneRevisionBroadcastAndLog()
    {
        var ack = await _service.SetBudgetAsync(RequestA, Kid, _notepad, AppBudgetPart.Hours, 0, Device);

        Assert.Equal((2L, 1), (ack.Revision, _broadcasts.Count));
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("app rule changed: Editor, 0:00, no day", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetBudgetAsync_StorageFails_SnapshotUnchanged()
    {
        var repository = new Mock<IAppRuleRepository>();
        repository.Setup(r => r.LoadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new StoredAppRules([new StoredAppRecord(Kid, 1, Notepad, "Editor", 0, BreakTimeDays.None)], []));
        repository.Setup(r => r.SetRuleAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<BreakTimeDays>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("disk full", 13));
        using var service = Create(repository.Object);
        await service.InitializeAsync();

        await Assert.ThrowsAsync<SqliteException>(() => service.SetBudgetAsync(RequestA, Kid, 1, AppBudgetPart.Hours, 1, Device));

        Assert.Equal(0, service.Current.RuleFor(Kid, Notepad)!.BudgetMinutes);
        Assert.Equal(1, (await service.GetAsync(Kid)).Revision);
    }

    // ---------- Days ----------

    [Fact]
    public async Task SetDayAsync_TickAndUntick_AnyCombinationAlsoNone()
    {
        await _service.SetDayAsync(RequestA, Kid, _notepad, DayOfWeek.Monday, true, Device);
        await _service.SetDayAsync(RequestA, Kid, _notepad, DayOfWeek.Friday, true, Device);
        Assert.Equal(BreakTimeDays.Monday | BreakTimeDays.Friday, _service.Current.RuleFor(Kid, Notepad)!.Days);

        await _service.SetDayAsync(RequestA, Kid, _notepad, DayOfWeek.Monday, false, Device);
        await _service.SetDayAsync(RequestA, Kid, _notepad, DayOfWeek.Friday, false, Device);

        Assert.Equal(BreakTimeDays.None, _service.Current.RuleFor(Kid, Notepad)!.Days);
        Assert.DoesNotContain((await _repository.LoadAllAsync()).Apps, a => a.Days != BreakTimeDays.None);
    }

    [Fact]
    public async Task SetDayAsync_ConcurrentWrites_AllKept()
    {
        await Task.WhenAll(Enum.GetValues<DayOfWeek>().Select(d => _service.SetDayAsync(Guid.NewGuid(), Kid, _notepad, d, true, Device)));

        Assert.Equal(BreakTimeDays.All, _service.Current.RuleFor(Kid, Notepad)!.Days);
    }

    [Fact]
    public async Task SetDayAsync_UnknownAccount_Throws()
    {
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.SetDayAsync(RequestA, "S-1-5-21-1-2-3-1999", _notepad, DayOfWeek.Monday, true, Device));
    }

    [Fact]
    public async Task Writes_NullDeviceName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetDayAsync(RequestA, Kid, _notepad, DayOfWeek.Monday, true, null!));
    }

    // ---------- Budget text ----------

    [Fact]
    public async Task SetBudgetTextAsync_StoredNormalizedNotLogged()
    {
        await _service.SetBudgetTextAsync(RequestA, Kid, "Schluss\r\njetzt \U0001F60A", Device);

        Assert.Equal("Schluss\njetzt \U0001F60A", (await _service.GetAsync(Kid)).BudgetText);
        Assert.Equal("Schluss\njetzt \U0001F60A", (await _repository.LoadAllAsync()).BudgetTexts.Single().Text);
        Assert.DoesNotContain(_logger.Messages(LogLevel.Information), m => m.Contains("Schluss", StringComparison.Ordinal));
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains(": budget text changed (", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    [InlineData("Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.")]
    [InlineData(EnglishBudgetDefault)]
    public async Task SetBudgetTextAsync_EmptyOrAnyDefault_StoredAsDefault(string text)
    {
        await _service.SetBudgetTextAsync(RequestA, Kid, "eigener Text", Device);

        await _service.SetBudgetTextAsync(RequestB, Kid, text, Device);

        Assert.Null((await _service.GetAsync(Kid)).BudgetText);
        Assert.Empty((await _repository.LoadAllAsync()).BudgetTexts);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("budget text reset to the default", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetBudgetTextAsync_Invalid_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetBudgetTextAsync(RequestA, Kid, null!, Device));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetBudgetTextAsync(RequestA, Kid, new string('a', 501), Device));
    }

    [Fact]
    public async Task Broadcast_Fails_WarningAndWriteStillAcknowledged()
    {
        var failure = new InvalidOperationException("hub");
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AppRulesDto>())).ThrowsAsync(failure);

        var ack = await _service.SetBudgetTextAsync(RequestA, Kid, "x", Device);

        Assert.Equal((2L, true), (ack.Revision, _logger.Has(LogLevel.Warning, failure)));
    }

    // ---------- App records ----------

    [Fact]
    public async Task OnAppRecordAsync_NewRecord_DefaultRuleRevisionAndBroadcastNoLog()
    {
        var logs = _logger.Entries.Count;

        await _service.OnAppRecordAsync(Kid, 77, @"C:\Games\game.exe", "Game");

        Assert.Equal(new AppRuleEntry(77, @"C:\Games\game.exe", "Game", 0, BreakTimeDays.None), _service.Current.RuleFor(Kid, @"C:\Games\game.exe"));
        Assert.Equal((2L, 2), (_broadcasts.Single().Revision, _broadcasts.Single().Apps.Count));
        Assert.Equal(logs, _logger.Entries.Count);
    }

    [Fact]
    public async Task OnAppRecordAsync_Renamed_KeepsTheRuleAndBroadcasts()
    {
        await _service.SetBudgetAsync(RequestA, Kid, _notepad, AppBudgetPart.Minutes, 30, Device);

        await _service.OnAppRecordAsync(Kid, _notepad, Notepad, "Notepad");

        Assert.Equal(new AppRuleEntry(_notepad, Notepad, "Notepad", 30, BreakTimeDays.None), _service.Current.RuleFor(Kid, Notepad));
        Assert.Equal(2, _broadcasts.Count);
    }

    [Fact]
    public async Task OnAppRecordAsync_PathCaseChanged_Updated()
    {
        await _service.OnAppRecordAsync(Kid, _notepad, Notepad.ToUpperInvariant(), "Editor");

        Assert.Equal(Notepad.ToUpperInvariant(), _service.Current.For(Kid).Apps[_notepad].ProgramPath);
        Assert.Single(_service.Current.For(Kid).ByPath);
    }

    [Fact]
    public async Task OnAppRecordAsync_Unchanged_NothingBroadcast()
    {
        await _service.OnAppRecordAsync(Kid, _notepad, Notepad, "Editor");

        Assert.Empty(_broadcasts);
    }

    [Fact]
    public async Task OnAppRecordAsync_IgnoredProgram_NotListed()
    {
        await _service.OnAppRecordAsync(Kid, 5, Explorer, "Windows-Explorer");

        Assert.Empty(_broadcasts);
        Assert.Null(_service.Current.RuleFor(Kid, Explorer));
    }

    [Fact]
    public async Task OnAppRecordAsync_Guards_Throw()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.OnAppRecordAsync(" ", 1, Notepad, "x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.OnAppRecordAsync(Kid, 1, " ", "x"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.OnAppRecordAsync(Kid, 1, Notepad, null!));
    }

    // ---------- Purge ----------

    [Fact]
    public async Task PurgeMissingAccountsAsync_DropsAccountAndText_OtherAccountKept()
    {
        await _service.SetBudgetTextAsync(RequestA, Kid, "a", Device);
        await _service.SetBudgetTextAsync(RequestA, Kid2, "b", Device);

        await _service.PurgeMissingAccountsAsync([Kid2]);

        Assert.True(_service.Current.For(Kid).IsEmpty);
        Assert.Equal("b", _service.Current.For(Kid2).BudgetText);
        Assert.Equal([new StoredDisplayText(Kid2, "b")], (await _repository.LoadAllAsync()).BudgetTexts);
        Assert.Contains($"Purged the budget text of deleted account {Kid}.", _logger.Messages(LogLevel.Information));
        Assert.Equal(RequestA, (await _service.GetAsync(Kid2)).LastChangeRequestId);
    }

    [Fact]
    public async Task PurgeMissingAccountsAsync_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.PurgeMissingAccountsAsync(null!));
    }

    private AppRuleService Create(IAppRuleRepository repository) =>
        new(repository, _broadcaster.Object, _accounts.Object, new EnforcementIgnoreList(@"C:\Windows"), _time, _logger);
}
