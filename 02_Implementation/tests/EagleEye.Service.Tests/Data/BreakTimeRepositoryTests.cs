using EagleEye.Service.Data;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class BreakTimeRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly BreakTimeEntryDto Defaults = new(0, false, 1200, 1439, BreakTimeDays.All);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly BreakTimeRepository _repository;

    public BreakTimeRepositoryTests()
    {
        _repository = new BreakTimeRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LoadAll_Empty()
    {
        var stored = await _repository.LoadAllAsync();

        Assert.Equal((0, 0), (stored.Entries.Count, stored.Texts.Count));
    }

    [Fact]
    public async Task InsertEntry_LoadsInCreationOrderPerAccount()
    {
        var first = await _repository.InsertEntryAsync(Kid, Defaults, Now);
        var other = await _repository.InsertEntryAsync(Kid2, Defaults with { IsActive = true }, Now);
        var second = await _repository.InsertEntryAsync(Kid, Defaults with { StartMinute = 0, EndMinute = 540, Days = BreakTimeDays.Saturday }, Now);

        var stored = await _repository.LoadAllAsync();

        Assert.Equal(
            [
                new StoredBreakTimeEntry(Kid, Defaults with { EntryId = first }),
                new StoredBreakTimeEntry(Kid2, Defaults with { EntryId = other, IsActive = true }),
                new StoredBreakTimeEntry(Kid, new BreakTimeEntryDto(second, false, 0, 540, BreakTimeDays.Saturday)),
            ],
            stored.Entries);
        Assert.True(first < other && other < second);
    }

    [Fact]
    public async Task UpdateEntry_Existing_StoresValues()
    {
        var id = await _repository.InsertEntryAsync(Kid, Defaults, Now);
        var changed = new BreakTimeEntryDto(id, true, 840, 900, BreakTimeDays.Friday);

        var updated = await _repository.UpdateEntryAsync(Kid.ToLowerInvariant(), changed, Now.AddMinutes(1));

        Assert.True(updated);
        Assert.Equal(changed, (await _repository.LoadAllAsync()).Entries.Single().Entry);
    }

    [Fact]
    public async Task UpdateEntry_MissingOrOtherAccount_False()
    {
        var id = await _repository.InsertEntryAsync(Kid, Defaults, Now);

        Assert.False(await _repository.UpdateEntryAsync(Kid, Defaults with { EntryId = id + 1 }, Now));
        Assert.False(await _repository.UpdateEntryAsync(Kid2, Defaults with { EntryId = id }, Now));
    }

    [Fact]
    public async Task DeleteEntry_ExistingThenMissing()
    {
        var id = await _repository.InsertEntryAsync(Kid, Defaults, Now);

        Assert.False(await _repository.DeleteEntryAsync(Kid2, id));
        Assert.True(await _repository.DeleteEntryAsync(Kid, id));
        Assert.False(await _repository.DeleteEntryAsync(Kid, id));
        Assert.Empty((await _repository.LoadAllAsync()).Entries);
    }

    [Theory]
    [InlineData(1200, 1200, 127)]
    [InlineData(1200, 1100, 127)]
    [InlineData(1439, 1439, 127)]
    [InlineData(0, 1440, 127)]
    [InlineData(0, 60, 0)]
    [InlineData(0, 60, 128)]
    public async Task InsertEntry_InvalidValues_RejectedByTheSchema(int start, int end, int days)
    {
        await Assert.ThrowsAsync<SqliteException>(
            () => _repository.InsertEntryAsync(Kid, new BreakTimeEntryDto(0, true, start, end, (BreakTimeDays)days), Now));
    }

    [Fact]
    public async Task SetDisplayText_InsertReplaceDelete()
    {
        await _repository.SetDisplayTextAsync(Kid, "Erst \U0001F60A", Now);
        await _repository.SetDisplayTextAsync(Kid, "Dann\n\U0001F468\u200D\U0001F469\u200D\U0001F467", Now.AddMinutes(1));
        await _repository.SetDisplayTextAsync(Kid2, "Anna", Now);

        Assert.Equal(
            [new StoredDisplayText(Kid, "Dann\n\U0001F468\u200D\U0001F469\u200D\U0001F467"), new StoredDisplayText(Kid2, "Anna")],
            (await _repository.LoadAllAsync()).Texts);

        await _repository.SetDisplayTextAsync(Kid.ToLowerInvariant(), null, Now);

        Assert.Equal([new StoredDisplayText(Kid2, "Anna")], (await _repository.LoadAllAsync()).Texts);
    }

    [Fact]
    public async Task PurgeAccountsNotIn_DeletesEntriesAndTextsOfMissingSids()
    {
        await _repository.InsertEntryAsync(Kid, Defaults, Now);
        await _repository.InsertEntryAsync(Kid, Defaults, Now);
        await _repository.InsertEntryAsync(Kid2, Defaults, Now);
        await _repository.SetDisplayTextAsync(Kid2, "Anna", Now);
        await _repository.SetDisplayTextAsync("S-1-5-21-1-2-3-1005", "only text", Now);

        var purged = await _repository.PurgeAccountsNotInAsync([Kid2.ToLowerInvariant()]);

        Assert.Equal([new RulesPurge(Kid, 2, false), new RulesPurge("S-1-5-21-1-2-3-1005", 0, true)], purged);
        var stored = await _repository.LoadAllAsync();
        Assert.Equal((Kid2, 1, Kid2), (stored.Entries.Single().AccountSid, stored.Entries.Count, stored.Texts.Single().AccountSid));
    }

    [Fact]
    public async Task PurgeAccountsNotIn_EmptyList_DeletesAll()
    {
        await _repository.InsertEntryAsync(Kid, Defaults, Now);

        Assert.Single(await _repository.PurgeAccountsNotInAsync([]));
        Assert.Empty((await _repository.LoadAllAsync()).Entries);
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.InsertEntryAsync(" ", Defaults, Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.InsertEntryAsync(Kid, null!, Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.UpdateEntryAsync(" ", Defaults, Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.UpdateEntryAsync(Kid, null!, Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.DeleteEntryAsync(" ", 1));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetDisplayTextAsync(" ", "x", Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeAccountsNotInAsync(null!));
    }

    [Fact]
    public async Task Migration4_OnVersion3File_KeepsAllEarlierData()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await CreateVersion3FileAsync(file);

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();

            Assert.Equal(4, await database.GetSchemaVersionAsync());
            Assert.NotNull(await new PairedDeviceRepository(database).FindByTokenHashAsync(new byte[32]));
            Assert.True((await new AccountSelectionRepository(database).LoadAllAsync())[Kid]);
            var usage = await new UsageRepository(database).GetUsageAsync(Kid, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9));
            Assert.Equal(605, usage.Single().Seconds);
            Assert.Empty((await new BreakTimeRepository(database).LoadAllAsync()).Entries);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task CreateVersion3FileAsync(string file)
    {
        await using var connection = new SqliteConnection(ServiceDatabase.BuildConnectionString(file));
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE SchemaVersion (Version INTEGER NOT NULL);
            INSERT INTO SchemaVersion (Version) VALUES (3);
            CREATE TABLE PairedDevices (DeviceId TEXT PRIMARY KEY NOT NULL, DeviceName TEXT NOT NULL, TokenHash BLOB NOT NULL UNIQUE, PairedAtUtc TEXT NOT NULL);
            INSERT INTO PairedDevices VALUES ('device-1', 'Dad', zeroblob(32), '2026-10-04T12:00:00.0000000+00:00');
            CREATE TABLE AccountSelections (Sid TEXT PRIMARY KEY NOT NULL, UserName TEXT NOT NULL, UnderParentalControl INTEGER NOT NULL, ChangedAtUtc TEXT NOT NULL);
            INSERT INTO AccountSelections VALUES ('{Kid}', 'kid1', 1, '2026-10-07T12:00:00.0000000+00:00');
            CREATE TABLE AppRecords (AppId INTEGER PRIMARY KEY AUTOINCREMENT, AccountSid TEXT NOT NULL, ProgramPath TEXT NOT NULL COLLATE NOCASE,
                ProcessName TEXT NOT NULL, DisplayName TEXT NOT NULL, FirstSeenUtc TEXT NOT NULL, LastSeenUtc TEXT NOT NULL, UNIQUE (AccountSid, ProgramPath));
            CREATE TABLE AppInstances (InstanceId INTEGER PRIMARY KEY AUTOINCREMENT, AppId INTEGER NOT NULL REFERENCES AppRecords (AppId) ON DELETE CASCADE,
                StartedUtc TEXT NOT NULL, LastSeenUtc TEXT NOT NULL, EndedUtc TEXT NULL, EndReason TEXT NULL);
            CREATE TABLE DailyUsage (AppId INTEGER NOT NULL REFERENCES AppRecords (AppId) ON DELETE CASCADE, Day TEXT NOT NULL,
                Seconds INTEGER NOT NULL CHECK (Seconds >= 0), PRIMARY KEY (AppId, Day));
            INSERT INTO AppRecords VALUES (1, '{Kid}', 'C:\Windows\notepad.exe', 'notepad.exe', 'Editor', '2026-10-09T08:00:00.0000000+00:00', '2026-10-09T08:10:00.0000000+00:00');
            INSERT INTO DailyUsage VALUES (1, '2026-10-09', 605);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
