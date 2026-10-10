using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class BreakTimeServiceTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Device = "Dad's laptop";
    private static readonly Guid RequestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RequestB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly BreakTimeRepository _repository;
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly List<AccountRulesDto> _broadcasts = [];
    private readonly Mock<IAccountRulesBroadcaster> _broadcaster = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly TestLogger<BreakTimeService> _logger = new();
    private BreakTimeService _service;

    public BreakTimeServiceTests()
    {
        _repository = new BreakTimeRepository(_database);
        _accounts.Setup(a => a.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserAccountListDto(1, null,
        [
            new UserAccountDto(Kid, "kid1", null, false, true),
            new UserAccountDto(Kid2, "kid2", null, false, false),
        ]));
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AccountRulesDto>())).Callback<AccountRulesDto>(_broadcasts.Add).Returns(Task.CompletedTask);
        _service = Create(_repository);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _service.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    // ---------- Initialize / Get ----------

    [Fact]
    public async Task InitializeAsync_LoadsStoredRulesIntoCurrentAndLogs()
    {
        var id = await _repository.InsertEntryAsync(Kid, new BreakTimeEntryDto(0, true, 60, 120, BreakTimeDays.Monday), _time.GetUtcNow());
        await _repository.SetDisplayTextAsync(Kid2, "Anna", _time.GetUtcNow());

        await _service.InitializeAsync();

        Assert.Equal([new BreakTimeEntryDto(id, true, 60, 120, BreakTimeDays.Monday)], _service.Current.For(Kid).Entries);
        Assert.Equal("Anna", _service.Current.For(Kid2).DisplayText);
        Assert.Contains("Break times loaded: 1 entries (1 on) of 1 accounts, 1 changed display texts.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task GetAsync_NothingStored_DefaultText()
    {
        var dto = await _service.GetAsync(Kid.ToLowerInvariant());

        Assert.Equal(new AccountRulesDto(1, null, Kid, [], BreakTimeRules.DefaultDisplayText, true) with { BreakTimes = dto.BreakTimes }, dto);
        Assert.Empty(dto.BreakTimes);
    }

    [Fact]
    public async Task GetAsync_UnknownAccount_Throws()
    {
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.GetAsync("S-1-5-21-1-2-3-1999"));
    }

    [Fact]
    public async Task GetAsync_AfterWrite_HasRevisionAndRequestOfTheAccount()
    {
        await _service.AddEntryAsync(RequestA, Kid, Device);

        var kid = await _service.GetAsync(Kid);
        var kid2 = await _service.GetAsync(Kid2);

        Assert.Equal((2L, (Guid?)RequestA, 2L, (Guid?)null), (kid.Revision, kid.LastChangeRequestId, kid2.Revision, kid2.LastChangeRequestId));
    }

    // ---------- Add / delete ----------

    [Fact]
    public async Task AddEntryAsync_DefaultsAtTheEnd_RevisionBroadcastLog()
    {
        await _service.AddEntryAsync(RequestA, Kid, Device);
        var ack = await _service.AddEntryAsync(RequestB, Kid, Device);

        var entries = _service.Current.For(Kid).Entries;
        Assert.Equal((3L, 2), (ack.Revision, entries.Count));
        Assert.All(entries, e => Assert.Equal((false, 1200, 1439, BreakTimeDays.All), (e.IsActive, e.StartMinute, e.EndMinute, e.Days)));
        Assert.True(entries[0].EntryId < entries[1].EntryId);
        Assert.Equal((3L, (Guid?)RequestB, 2), (_broadcasts[^1].Revision, _broadcasts[^1].LastChangeRequestId, _broadcasts[^1].BreakTimes.Count));
        Assert.Contains(
            $"Break times of account kid1 ({Kid}) changed by {Device}: entry {entries[1].EntryId} added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su) (request {RequestB}, revision 3).",
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task AddEntryAsync_AtTheLimit_ThrowsTooManyEntriesWithoutRevision()
    {
        for (var i = 0; i < BreakTimeRules.MaxEntriesPerAccount; i++)
        {
            await _service.AddEntryAsync(Guid.NewGuid(), Kid, Device);
        }

        await Assert.ThrowsAsync<TooManyEntriesException>(() => _service.AddEntryAsync(RequestA, Kid, Device));

        Assert.Equal((21L, 20), ((await _service.GetAsync(Kid)).Revision, _broadcasts.Count));
    }

    [Fact]
    public async Task DeleteEntryAsync_RemovesTheRow()
    {
        var id = await AddAsync(Kid);
        var other = await AddAsync(Kid);

        await _service.DeleteEntryAsync(RequestA, Kid, id, Device);

        Assert.Equal([other], _service.Current.For(Kid).Entries.Select(e => e.EntryId));
        Assert.Equal([other], (await _repository.LoadAllAsync()).Entries.Select(e => e.Entry.EntryId));
        Assert.Contains($"entry {id} deleted", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteEntryAsync_MissingOrOtherAccount_EntryNotFound()
    {
        var id = await AddAsync(Kid);

        await Assert.ThrowsAsync<EntryNotFoundException>(() => _service.DeleteEntryAsync(RequestA, Kid, id + 100, Device));
        await Assert.ThrowsAsync<EntryNotFoundException>(() => _service.DeleteEntryAsync(RequestA, Kid2, id, Device));
    }

    [Fact]
    public async Task DeleteEntryAsync_RepositoryFindsNothing_EntryNotFound()
    {
        var repository = MockRepositoryWithOneEntry(out var id);
        repository.Setup(r => r.DeleteEntryAsync(Kid, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = Create(repository.Object);
        await service.InitializeAsync();

        await Assert.ThrowsAsync<EntryNotFoundException>(() => service.DeleteEntryAsync(RequestA, Kid, id, Device));
    }

    // ---------- Field writes ----------

    [Fact]
    public async Task SetActiveAsync_StoresAndLogsAllValues()
    {
        var id = await AddAsync(Kid);

        await _service.SetActiveAsync(RequestA, Kid, id, true, Device);

        Assert.True(_service.Current.For(Kid).Entries.Single().IsActive);
        Assert.True((await _repository.LoadAllAsync()).Entries.Single().Entry.IsActive);
        Assert.Contains($"entry {id} changed: on, 20:00–23:59, Mo Tu We Th Fr Sa Su", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetActiveAsync_SameValue_StillOneRevisionAndBroadcast()
    {
        var id = await AddAsync(Kid);

        var ack = await _service.SetActiveAsync(RequestA, Kid, id, false, Device);

        Assert.Equal((3L, 2), (ack.Revision, _broadcasts.Count));
    }

    [Theory]
    [InlineData(BreakTimeBoundary.Start, 0, 0, 1439)]
    [InlineData(BreakTimeBoundary.Start, 1438, 1438, 1439)]
    [InlineData(BreakTimeBoundary.End, 1201, 1200, 1201)]
    public async Task SetTimeAsync_Valid_Stored(BreakTimeBoundary boundary, int minute, int start, int end)
    {
        var id = await AddAsync(Kid);

        await _service.SetTimeAsync(RequestA, Kid, id, boundary, minute, Device);

        Assert.Equal((start, end), (_service.Current.For(Kid).Entries.Single().StartMinute, _service.Current.For(Kid).Entries.Single().EndMinute));
    }

    [Theory]
    [InlineData(BreakTimeBoundary.Start, 1439)]
    [InlineData(BreakTimeBoundary.End, 1200)]
    [InlineData(BreakTimeBoundary.End, 1140)]
    public async Task SetTimeAsync_EndNotAfterStart_RejectedNothingChanged(BreakTimeBoundary boundary, int minute)
    {
        var id = await AddAsync(Kid);

        var ex = await Assert.ThrowsAsync<RuleValidationException>(() => _service.SetTimeAsync(RequestA, Kid, id, boundary, minute, Device));

        Assert.Equal((RuleViolation.EndNotAfterStart, 1, 1200, 1439), (ex.Violation, _broadcasts.Count, _service.Current.For(Kid).Entries.Single().StartMinute, _service.Current.For(Kid).Entries.Single().EndMinute));
    }

    [Fact]
    public async Task SetTimeAsync_RaceWithOtherBoundary_ValidatedAgainstStoredValues()
    {
        // App A moved the end to 09:00 (start still 20:00 is invalid, so first the start goes to 08:00); app B then sends start 10:00.
        var id = await AddAsync(Kid);
        await _service.SetTimeAsync(RequestA, Kid, id, BreakTimeBoundary.Start, 480, Device);
        await _service.SetTimeAsync(RequestA, Kid, id, BreakTimeBoundary.End, 540, Device);

        await Assert.ThrowsAsync<RuleValidationException>(() => _service.SetTimeAsync(RequestB, Kid, id, BreakTimeBoundary.Start, 600, Device));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1440)]
    public async Task SetTimeAsync_MinuteOutOfRange_Throws(int minute)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SetTimeAsync(RequestA, Kid, 1, BreakTimeBoundary.Start, minute, Device));
    }

    [Fact]
    public async Task SetDayAsync_UntickAndTick()
    {
        var id = await AddAsync(Kid);

        await _service.SetDayAsync(RequestA, Kid, id, DayOfWeek.Sunday, false, Device);
        await _service.SetDayAsync(RequestA, Kid, id, DayOfWeek.Saturday, false, Device);
        await _service.SetDayAsync(RequestA, Kid, id, DayOfWeek.Sunday, true, Device);

        Assert.Equal(BreakTimeDays.All & ~BreakTimeDays.Saturday, _service.Current.For(Kid).Entries.Single().Days);
    }

    [Fact]
    public async Task SetDayAsync_LastDay_RejectedWithNoDaySelected()
    {
        var id = await AddAsync(Kid);
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday })
        {
            await _service.SetDayAsync(RequestA, Kid, id, day, false, Device);
        }

        var ex = await Assert.ThrowsAsync<RuleValidationException>(() => _service.SetDayAsync(RequestA, Kid, id, DayOfWeek.Sunday, false, Device));

        Assert.Equal((RuleViolation.NoDaySelected, BreakTimeDays.Sunday), (ex.Violation, _service.Current.For(Kid).Entries.Single().Days));
    }

    [Fact]
    public async Task FieldWrite_DeletedEntry_EntryNotFound()
    {
        var id = await AddAsync(Kid);
        await _service.DeleteEntryAsync(RequestA, Kid, id, Device);

        await Assert.ThrowsAsync<EntryNotFoundException>(() => _service.SetActiveAsync(RequestB, Kid, id, true, Device));
    }

    [Fact]
    public async Task FieldWrite_RepositoryFindsNothing_EntryNotFound()
    {
        var repository = MockRepositoryWithOneEntry(out var id);
        repository.Setup(r => r.UpdateEntryAsync(Kid, It.IsAny<BreakTimeEntryDto>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = Create(repository.Object);
        await service.InitializeAsync();

        await Assert.ThrowsAsync<EntryNotFoundException>(() => service.SetActiveAsync(RequestA, Kid, id, true, Device));
    }

    [Fact]
    public async Task FieldWrites_DifferentFieldsConcurrently_AllKept()
    {
        var id = await AddAsync(Kid);

        await Task.WhenAll(
            _service.SetActiveAsync(RequestA, Kid, id, true, Device),
            _service.SetDayAsync(RequestB, Kid, id, DayOfWeek.Monday, false, Device),
            _service.SetTimeAsync(Guid.NewGuid(), Kid, id, BreakTimeBoundary.Start, 1080, Device));

        Assert.Equal(new BreakTimeEntryDto(id, true, 1080, 1439, BreakTimeDays.All & ~BreakTimeDays.Monday), _service.Current.For(Kid).Entries.Single());
        Assert.Equal([2L, 3L, 4L, 5L], _broadcasts.Select(b => b.Revision).Order());
    }

    // ---------- Display text ----------

    [Fact]
    public async Task SetDisplayTextAsync_NormalizesLineBreaksAndKeepsEmojis()
    {
        await _service.SetDisplayTextAsync(RequestA, Kid, "Hallo\r\n\U0001F468‍\U0001F469‍\U0001F467\rEnde", Device);

        var dto = await _service.GetAsync(Kid);
        Assert.Equal(("Hallo\n\U0001F468‍\U0001F469‍\U0001F467\nEnde", false), (dto.DisplayText, dto.IsDefaultDisplayText));
        Assert.Equal("Hallo\n\U0001F468‍\U0001F469‍\U0001F467\nEnde", (await _repository.LoadAllAsync()).Texts.Single().Text);
        Assert.EndsWith("display text changed (request aaaaaaaa-0000-0000-0000-000000000001, revision 2).", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n\t ")]
    [InlineData(BreakTimeRules.DefaultDisplayText)]
    public async Task SetDisplayTextAsync_EmptyOrDefault_RestoresTheDefault(string text)
    {
        await _service.SetDisplayTextAsync(RequestA, Kid, "eigener Text", Device);

        await _service.SetDisplayTextAsync(RequestB, Kid, text, Device);

        Assert.Equal((BreakTimeRules.DefaultDisplayText, true), ((await _service.GetAsync(Kid)).DisplayText, (await _service.GetAsync(Kid)).IsDefaultDisplayText));
        Assert.Empty((await _repository.LoadAllAsync()).Texts);
        Assert.Contains("display text reset to the default", _logger.Messages(LogLevel.Information).Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetDisplayTextAsync_Invalid_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetDisplayTextAsync(RequestA, Kid, new string('a', 501), Device));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetDisplayTextAsync(RequestA, Kid, null!, Device));
    }

    [Fact]
    public async Task SetDisplayTextAsync_CrLfCountsAsOneCharacter()
    {
        await _service.SetDisplayTextAsync(RequestA, Kid, new string('a', 498) + "\r\n", Device);

        Assert.Equal(499, (await _service.GetAsync(Kid)).DisplayText.Length);
    }

    // ---------- Failures ----------

    [Fact]
    public async Task Write_UnknownAccount_ThrowsWithoutRevision()
    {
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.AddEntryAsync(RequestA, "S-1-5-21-1-2-3-1999", Device));

        Assert.Empty(_broadcasts);
    }

    [Fact]
    public async Task Write_AccountOfOtherCase_StoredUnderTheInventorySid()
    {
        await _service.AddEntryAsync(RequestA, Kid.ToLowerInvariant(), Device);

        Assert.Equal(Kid, (await _repository.LoadAllAsync()).Entries.Single().AccountSid);
    }

    [Fact]
    public async Task Write_BroadcastFails_WarningOnlyWriteSucceeds()
    {
        var failure = new InvalidOperationException("send failed");
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AccountRulesDto>())).ThrowsAsync(failure);

        var ack = await _service.AddEntryAsync(RequestA, Kid, Device);

        Assert.Equal((2L, true), (ack.Revision, _logger.Has(LogLevel.Warning, failure)));
    }

    [Fact]
    public async Task Write_StorageFails_ExceptionSnapshotAndRevisionUnchanged()
    {
        var repository = MockRepositoryWithOneEntry(out var id);
        repository.Setup(r => r.UpdateEntryAsync(Kid, It.IsAny<BreakTimeEntryDto>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("disk full", 13));
        var service = Create(repository.Object);
        await service.InitializeAsync();

        await Assert.ThrowsAsync<SqliteException>(() => service.SetActiveAsync(RequestA, Kid, id, true, Device));

        Assert.Equal((false, 1L), (service.Current.For(Kid).Entries.Single().IsActive, (await service.GetAsync(Kid)).Revision));
    }

    [Fact]
    public async Task Write_NullDeviceName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.AddEntryAsync(RequestA, Kid, null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.AddEntryAsync(RequestA, " ", Device));
    }

    // ---------- Purge ----------

    [Fact]
    public async Task PurgeMissingAccountsAsync_DeletesRulesOfDeletedAccountsAndLogs()
    {
        await AddAsync(Kid);
        await _service.SetDisplayTextAsync(RequestA, Kid2, "Anna", Device);

        await _service.PurgeMissingAccountsAsync([Kid2]);

        Assert.Equal((0, "Anna"), (_service.Current.For(Kid).Entries.Count, _service.Current.For(Kid2).DisplayText));
        Assert.Contains($"Purged the break times of deleted account {Kid}: 1 entries, display text not changed.", _logger.Messages(LogLevel.Information));
        await _service.PurgeMissingAccountsAsync([]);
        Assert.Contains($"Purged the break times of deleted account {Kid2}: 0 entries, display text deleted.", _logger.Messages(LogLevel.Information));
        Assert.Null((await _service.GetAsync(Kid2)).LastChangeRequestId);
    }

    [Fact]
    public async Task PurgeMissingAccountsAsync_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.PurgeMissingAccountsAsync(null!));
    }

    private async Task<long> AddAsync(string sid)
    {
        await _service.AddEntryAsync(Guid.NewGuid(), sid, Device);
        return _service.Current.For(sid).Entries[^1].EntryId;
    }

    private static Mock<IBreakTimeRepository> MockRepositoryWithOneEntry(out long id)
    {
        id = 7;
        var repository = new Mock<IBreakTimeRepository>();
        repository.Setup(r => r.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredRules([new StoredBreakTimeEntry(Kid, new BreakTimeEntryDto(id, false, 1200, 1439, BreakTimeDays.All))], []));
        return repository;
    }

    private BreakTimeService Create(IBreakTimeRepository repository) =>
        new(repository, _broadcaster.Object, _accounts.Object, _time, _logger);
}
