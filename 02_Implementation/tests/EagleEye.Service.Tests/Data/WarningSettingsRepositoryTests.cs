using EagleEye.Service.Data;
using EagleEye.Shared.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class WarningSettingsRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 18, 0, 0, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly WarningSettingsRepository _repository;

    public WarningSettingsRepositoryTests()
    {
        _repository = new WarningSettingsRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Load_NoRow_Null()
    {
        Assert.Null(await _repository.LoadAsync());
    }

    [Fact]
    public async Task Save_InsertThenReplace()
    {
        await _repository.SaveAsync(new StoredWarningSettings(5, 1), Now);
        await _repository.SaveAsync(new StoredWarningSettings(3, 2), Now);

        Assert.Equal(new StoredWarningSettings(3, 2), await _repository.LoadAsync());
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    [InlineData(60, 1)]
    [InlineData(5, 0)]
    public async Task Save_InvalidLevels_RejectedBySchema(int early, int final)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _repository.SaveAsync(new StoredWarningSettings(early, final), Now));
    }

    [Fact]
    public async Task Save_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!, Now));
    }

    [Fact]
    public async Task Migration7_FreshDatabase_SchemaSevenWithoutTexts()
    {
        Assert.Equal(7, await _database.GetSchemaVersionAsync());
        Assert.Equal(["Id", "EarlyMinutes", "FinalMinutes", "ChangedUtc"], await ColumnsAsync(_database));
    }

    [Fact]
    public async Task Migration7_OnVersion6File_DropsTheTextsAndKeepsTheMinutes()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await CreateVersion6FileAsync(file);

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();

            Assert.Equal(7, await database.GetSchemaVersionAsync());
            Assert.Equal(["Id", "EarlyMinutes", "FinalMinutes", "ChangedUtc"], await ColumnsAsync(database));
            Assert.Equal(new StoredWarningSettings(7, 2), await new WarningSettingsRepository(database).LoadAsync());
            Assert.True((await new AccountSelectionRepository(database).LoadAllAsync())["S-1-5-21-1-2-3-1003"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    private static async Task CreateVersion6FileAsync(string file)
    {
        await using (var version6 = new Version6Database(ServiceDatabase.BuildConnectionString(file)))
        {
            await version6.InitializeAsync();
            await version6.ExecuteAsync(async (connection, token) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO AccountSelections VALUES ('S-1-5-21-1-2-3-1003', 'kid1', 1, '2026-10-07T12:00:00.0000000+00:00');
                    INSERT INTO WarningSettings (Id, EarlyMinutes, EarlyText, FinalMinutes, FinalText, ChangedUtc)
                        VALUES (1, 7, 'Bald Schluss', 2, 'Jetzt speichern 🚀', '2026-10-30T08:00:00.0000000+00:00');
                    """;
                return await command.ExecuteNonQueryAsync(token);
            });
        }

        SqliteConnection.ClearAllPools();
    }

    private static Task<List<string>> ColumnsAsync(ServiceDatabase database)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM pragma_table_info('WarningSettings') ORDER BY cid;";
            using var reader = await command.ExecuteReaderAsync(token);
            var names = new List<string>();
            while (await reader.ReadAsync(token))
            {
                names.Add(reader.GetString(0));
            }

            return names;
        });
    }

    /// <summary>The service database as it was in 0.9.0: migrations 1 to 6.</summary>
    private sealed class Version6Database(string connectionString) : SqliteDatabase(connectionString)
    {
        protected override IReadOnlyList<string> Migrations => [.. ServiceDatabase.AllMigrations.Take(6)];
    }
}
