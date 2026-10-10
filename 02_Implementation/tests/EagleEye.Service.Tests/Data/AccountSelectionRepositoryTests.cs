using EagleEye.Service.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class AccountSelectionRepositoryTests : IAsyncLifetime
{
    private const string Max = "S-1-5-21-1-2-3-1001";
    private const string Anna = "S-1-5-21-1-2-3-1002";
    private const string Lena = "S-1-5-21-1-2-3-1003";
    private static readonly DateTimeOffset ChangedAt = new(2026, 10, 8, 19, 42, 7, TimeSpan.FromHours(2));

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AccountSelectionRepository _repository;

    public AccountSelectionRepositoryTests()
    {
        _repository = new AccountSelectionRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task InitializeAsync_NewDatabase_IsAtLatestSchemaVersion()
    {
        Assert.Equal(3, await _database.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task LoadAllAsync_Empty_ReturnsNoSelections()
    {
        Assert.Empty(await _repository.LoadAllAsync());
    }

    [Fact]
    public async Task SetAsync_New_IsLoaded()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);
        await _repository.SetAsync(Anna, "anna", false, ChangedAt);

        var all = await _repository.LoadAllAsync();

        Assert.Equal((2, true, false), (all.Count, all[Max], all[Anna]));
    }

    [Fact]
    public async Task LoadAllAsync_LookupIgnoresCase()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);

        Assert.True((await _repository.LoadAllAsync())[Max.ToLowerInvariant()]);
    }

    [Fact]
    public async Task SetAsync_Existing_UpdatesValueNameAndTime()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);

        await _repository.SetAsync(Max, "maximilian", false, ChangedAt.AddMinutes(1));

        Assert.Equal(("maximilian", 0L, "2026-10-08T17:43:07.0000000+00:00"), await ReadRowAsync(Max));
    }

    [Fact]
    public async Task SetAsync_StoresUtcTimeInIsoFormat()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);

        Assert.Equal(("max", 1L, "2026-10-08T17:42:07.0000000+00:00"), await ReadRowAsync(Max));
    }

    [Fact]
    public async Task DeleteMissingAsync_DeletesOnlyMissingAndReturnsThem()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);
        await _repository.SetAsync(Anna, "anna", true, ChangedAt);
        await _repository.SetAsync(Lena, "lena", false, ChangedAt);

        var deleted = await _repository.DeleteMissingAsync([Max, "S-1-5-21-1-2-3-500"]);

        Assert.Equal([Anna, Lena], deleted.Order(StringComparer.Ordinal));
        Assert.Equal([Max], (await _repository.LoadAllAsync()).Keys);
    }

    [Fact]
    public async Task DeleteMissingAsync_NothingMissing_ReturnsEmpty()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);

        Assert.Empty(await _repository.DeleteMissingAsync([Max, Anna]));
    }

    [Fact]
    public async Task DeleteMissingAsync_EmptyExistingSet_DeletesAll()
    {
        await _repository.SetAsync(Max, "max", true, ChangedAt);

        Assert.Equal([Max], await _repository.DeleteMissingAsync([]));
    }

    [Fact]
    public async Task SetAsync_InvalidValues_ThrowArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetAsync(" ", "max", true, ChangedAt));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetAsync(Max, "", true, ChangedAt));
    }

    [Fact]
    public async Task DeleteMissingAsync_Null_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.DeleteMissingAsync(null!));
    }

    [Fact]
    public async Task Migrations_OnExistingVersion1Database_KeepPairedDevices()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await CreateVersion1DatabaseAsync(file);

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();
            var found = await new PairedDeviceRepository(database).FindByTokenHashAsync(new byte[32]);

            Assert.Equal((3L, "device-1"), (await database.GetSchemaVersionAsync(), found?.DeviceId));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task CreateVersion1DatabaseAsync(string file)
    {
        await using var connection = new SqliteConnection(ServiceDatabase.BuildConnectionString(file));
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE SchemaVersion (Version INTEGER NOT NULL);
            INSERT INTO SchemaVersion (Version) VALUES (1);
            CREATE TABLE PairedDevices (
                DeviceId TEXT PRIMARY KEY NOT NULL, DeviceName TEXT NOT NULL,
                TokenHash BLOB NOT NULL UNIQUE, PairedAtUtc TEXT NOT NULL);
            INSERT INTO PairedDevices VALUES ('device-1', 'Dad''s laptop', zeroblob(32), '2026-10-04T12:00:00.0000000+00:00');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private Task<(string UserName, long Value, string ChangedAt)> ReadRowAsync(string sid)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT UserName, UnderParentalControl, ChangedAtUtc FROM AccountSelections WHERE Sid = @sid;";
            command.Parameters.AddWithValue("@sid", sid);
            using var reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            return (reader.GetString(0), reader.GetInt64(1), reader.GetString(2));
        });
    }
}
