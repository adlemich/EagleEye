using EagleEye.Service.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class UsageRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\notepad.exe";
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly UsageRepository _repository;

    public UsageRepositoryTests()
    {
        _repository = new UsageRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task GetOrCreateApp_FirstTimeCreatedThenFoundCaseInsensitive()
    {
        var first = await App(Kid, Notepad);
        var second = await App(Kid.ToLowerInvariant(), Notepad.ToUpperInvariant(), "Notepad (new)");

        Assert.Equal((true, false, first.AppId), (first.Created, second.Created, second.AppId));
    }

    [Fact]
    public async Task GetOrCreateApp_OtherAccount_OwnRecord()
    {
        var first = await App(Kid, Notepad);
        var second = await App(Kid2, Notepad);

        Assert.True(second.Created);
        Assert.NotEqual(first.AppId, second.AppId);
    }

    [Fact]
    public async Task GetOrCreateApp_UpdatesDisplayName()
    {
        var app = await App(Kid, Notepad);
        await App(Kid, Notepad, "Editor (neu)");
        await _repository.ApplyAsync([new UsageIncrement(app.AppId, Today, 1)], []);

        Assert.Equal("Editor (neu)", (await _repository.GetUsageAsync(Kid, Today, Today)).Single().DisplayName);
    }

    [Fact]
    public async Task Instances_StartSeenEnd_OpenQuery()
    {
        var app = await App(Kid, Notepad);
        var first = await _repository.StartInstanceAsync(app.AppId, Now);
        var second = await _repository.StartInstanceAsync(app.AppId, Now.AddMinutes(1));
        await _repository.ApplyAsync([], [new InstanceSeenRecord(first, Now.AddMinutes(5)), new InstanceSeenRecord(second, Now.AddMinutes(5))]);
        await _repository.EndInstanceAsync(second, Now.AddMinutes(6), "closed");
        await _repository.ApplyAsync([], [new InstanceSeenRecord(second, Now.AddMinutes(9))]);
        await _repository.EndInstanceAsync(second, Now.AddMinutes(20), "again");

        var open = Assert.Single(await _repository.GetOpenInstancesAsync());

        Assert.Equal(
            new OpenInstanceRecord(first, Kid, Notepad, "notepad.exe", "Editor", Now, Now.AddMinutes(5)),
            open);
        Assert.Equal((Now.AddMinutes(6).ToString("O"), "closed"), await EndOf(second));
    }

    [Fact]
    public async Task Apply_UpsertAddsSeconds()
    {
        var app = await App(Kid, Notepad);
        await _repository.ApplyAsync([new UsageIncrement(app.AppId, Today, 5)], []);
        await _repository.ApplyAsync([new UsageIncrement(app.AppId, Today, 7), new UsageIncrement(app.AppId, Today.AddDays(-1), 0)], []);

        var usage = await _repository.GetUsageAsync(Kid, Today.AddDays(-1), Today);

        Assert.Equal([(Today, 12L), (Today.AddDays(-1), 0L)], usage.Select(u => (u.Day, u.Seconds)));
    }

    [Fact]
    public async Task Apply_FailureRollsBackEverything()
    {
        var app = await App(Kid, Notepad);

        await Assert.ThrowsAsync<SqliteException>(() =>
            _repository.ApplyAsync([new UsageIncrement(app.AppId, Today, 5), new UsageIncrement(999, Today, 5)], []));

        Assert.Empty(await _repository.GetUsageAsync(Kid, Today, Today));
    }

    [Fact]
    public async Task GetUsage_OnlyAccountAndRange()
    {
        var mine = await App(Kid, Notepad);
        var other = await App(Kid2, Notepad);
        await _repository.ApplyAsync(
            [new UsageIncrement(mine.AppId, Today, 1), new UsageIncrement(mine.AppId, Today.AddDays(-90), 1),
             new UsageIncrement(mine.AppId, Today.AddDays(1), 1), new UsageIncrement(other.AppId, Today, 1)],
            []);

        var usage = await _repository.GetUsageAsync(Kid.ToLowerInvariant(), Today.AddDays(-89), Today);

        Assert.Equal([(Today, mine.AppId, "Editor", 1L)], usage.Select(u => (u.Day, u.AppId, u.DisplayName, u.Seconds)));
    }

    [Fact]
    public async Task PurgeOlderThan_DeletesOldDaysAndEndedInstances()
    {
        var app = await App(Kid, Notepad);
        await _repository.ApplyAsync(
            [new UsageIncrement(app.AppId, Today.AddDays(-90), 1), new UsageIncrement(app.AppId, Today.AddDays(-89), 1)], []);
        var oldEnded = await _repository.StartInstanceAsync(app.AppId, Now.AddDays(-91));
        await _repository.EndInstanceAsync(oldEnded, Now.AddDays(-91), "closed");
        await _repository.StartInstanceAsync(app.AppId, Now.AddDays(-91));
        var recent = await _repository.StartInstanceAsync(app.AppId, Now.AddDays(-1));
        await _repository.EndInstanceAsync(recent, Now.AddDays(-1), "closed");

        var purged = await _repository.PurgeOlderThanAsync(Today.AddDays(-89), Now.AddDays(-90));

        Assert.Equal((1, 1), purged);
        Assert.Single(await _repository.GetUsageAsync(Kid, Today.AddDays(-100), Today));
        Assert.Single(await _repository.GetOpenInstancesAsync());
    }

    [Fact]
    public async Task PurgeAccountsNotIn_DeletesEverythingOfMissingAccounts()
    {
        var mine = await App(Kid, Notepad);
        await App(Kid, @"C:\x\game.exe");
        var other = await App(Kid2, Notepad);
        await _repository.ApplyAsync([new UsageIncrement(mine.AppId, Today, 1), new UsageIncrement(other.AppId, Today, 1)], []);
        await _repository.StartInstanceAsync(mine.AppId, Now);

        var purged = await _repository.PurgeAccountsNotInAsync([Kid2.ToLowerInvariant(), "S-1-5-21-9"]);

        Assert.Equal([new AccountPurge(Kid, 2, 1, 1)], purged);
        Assert.Empty(await _repository.GetUsageAsync(Kid, Today, Today));
        Assert.Empty(await _repository.GetOpenInstancesAsync());
        Assert.Single(await _repository.GetUsageAsync(Kid2, Today, Today));
    }

    [Fact]
    public async Task PurgeAccountsNotIn_EmptyList_DeletesAll()
    {
        await App(Kid, Notepad);

        Assert.Single(await _repository.PurgeAccountsNotInAsync([]));
    }

    [Fact]
    public async Task Migrations_OnVersion2File_KeepPairingsAndSelections()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await using (var connection = new SqliteConnection(ServiceDatabase.BuildConnectionString(file)))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SchemaVersion (Version INTEGER NOT NULL);
                    INSERT INTO SchemaVersion (Version) VALUES (2);
                    CREATE TABLE PairedDevices (DeviceId TEXT PRIMARY KEY NOT NULL, DeviceName TEXT NOT NULL, TokenHash BLOB NOT NULL UNIQUE, PairedAtUtc TEXT NOT NULL);
                    INSERT INTO PairedDevices VALUES ('device-1', 'Dad', zeroblob(32), '2026-10-04T12:00:00.0000000+00:00');
                    CREATE TABLE AccountSelections (Sid TEXT PRIMARY KEY NOT NULL, UserName TEXT NOT NULL, UnderParentalControl INTEGER NOT NULL, ChangedAtUtc TEXT NOT NULL);
                    INSERT INTO AccountSelections VALUES ('S-1-5-21-1-2-3-1003', 'kid1', 1, '2026-10-07T12:00:00.0000000+00:00');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();

            Assert.Equal(3, await database.GetSchemaVersionAsync());
            Assert.NotNull(await new PairedDeviceRepository(database).FindByTokenHashAsync(new byte[32]));
            Assert.True((await new AccountSelectionRepository(database).LoadAllAsync())["S-1-5-21-1-2-3-1003"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetOrCreateAppAsync(" ", Notepad, "n", "d", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetOrCreateAppAsync(Kid, " ", "n", "d", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetOrCreateAppAsync(Kid, Notepad, " ", "d", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetOrCreateAppAsync(Kid, Notepad, "n", " ", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.EndInstanceAsync(1, Now, " "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.ApplyAsync(null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.ApplyAsync([], null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.GetUsageAsync(" ", Today, Today));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeAccountsNotInAsync(null!));
    }

    private Task<AppRecordResult> App(string sid, string path, string display = "Editor") =>
        _repository.GetOrCreateAppAsync(sid, path, Path.GetFileName(path), display, Now);

    private Task<(string? End, string? Reason)> EndOf(long instanceId)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EndedUtc, EndReason FROM AppInstances WHERE InstanceId = @id;";
            command.Parameters.AddWithValue("@id", instanceId);
            using var reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            return ((string?)reader.GetString(0), (string?)reader.GetString(1));
        });
    }
}
