using EagleEye.Shared.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Shared.Tests.Data;

public sealed class SqliteDatabaseTests
{
    private const string InMemoryConnection = "Data Source=:memory:";

    [Fact]
    public async Task InitializeAsync_NewDatabase_AppliesAllMigrationsInOrder()
    {
        await using var db = TestDatabase.InMemory(
            "CREATE TABLE A (X INTEGER);",
            "ALTER TABLE A ADD COLUMN Y INTEGER;");

        await db.InitializeAsync();

        Assert.Equal(2, await db.GetSchemaVersionAsync());
        Assert.Equal(2, await db.CountColumnsAsync("A"));
    }

    [Fact]
    public async Task InitializeAsync_NoMigrations_StoresVersionZero()
    {
        await using var db = TestDatabase.InMemory();

        await db.InitializeAsync();

        Assert.Equal(0, await db.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task InitializeAsync_AppliesPragmas()
    {
        await using var db = TestDatabase.InMemory();
        await db.InitializeAsync();

        var pragmas = await db.ExecuteAsync(async (connection, ct) =>
            (await ScalarAsync(connection, "PRAGMA foreign_keys;"),
             await ScalarAsync(connection, "PRAGMA busy_timeout;"),
             await ScalarAsync(connection, "PRAGMA synchronous;")));

        // synchronous = NORMAL is reported as 1.
        Assert.Equal((1L, 5000L, 1L), pragmas);
    }

    [Fact]
    public async Task InitializeAsync_FailingMigration_RollsBackAndKeepsPreviousVersion()
    {
        var path = "file:" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";
        var connectionString = "Data Source=" + path;
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using var db = new TestDatabase(
            connectionString,
            "CREATE TABLE A (X INTEGER);",
            "CREATE TABLE B (X INTEGER); THIS IS NOT SQL;");

        await Assert.ThrowsAsync<SqliteException>(() => db.InitializeAsync());

        Assert.Equal((1L, 1L, 0L), (
            await ScalarAsync(keepAlive, "SELECT MAX(Version) FROM SchemaVersion;"),
            await ScalarAsync(keepAlive, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'A';"),
            await ScalarAsync(keepAlive, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'B';")));
    }

    [Fact]
    public async Task InitializeAsync_ExistingDatabase_AppliesOnlyNewMigrations()
    {
        var connectionString = "Data Source=file:" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        await using (var first = new TestDatabase(connectionString, "CREATE TABLE A (X INTEGER);"))
        {
            await first.InitializeAsync();
        }

        // Re-running migration 1 would fail with "table A already exists".
        await using var second = new TestDatabase(connectionString, "CREATE TABLE A (X INTEGER);", "CREATE TABLE B (X INTEGER);");
        await second.InitializeAsync();

        Assert.Equal(2, await second.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task ExecuteAsync_NotInitialized_ThrowsInvalidOperationException()
    {
        await using var db = TestDatabase.InMemory();

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task ExecuteAsync_NullWork_ThrowsArgumentNullException()
    {
        await using var db = TestDatabase.InMemory();

        await Assert.ThrowsAsync<ArgumentNullException>(() => db.ExecuteAsync<int>(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WorkThrows_ReleasesConnectionForNextCall()
    {
        await using var db = TestDatabase.InMemory();
        await db.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.ExecuteAsync<int>((_, _) => throw new InvalidOperationException("boom")));

        Assert.Equal(0, await db.GetSchemaVersionAsync());
    }

    [Fact]
    public void EnsureIntegrity_Ok_DoesNotThrow()
    {
        var exception = Record.Exception(() => SqliteDatabase.EnsureIntegrity("ok"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*** in database main ***")]
    public void EnsureIntegrity_NotOk_ThrowsInvalidDataException(string? result)
    {
        Assert.Throws<InvalidDataException>(() => SqliteDatabase.EnsureIntegrity(result));
    }

    [Fact]
    public void BuildConnectionString_FilePath_SetsDataSourceWithoutPooling()
    {
        var builder = new SqliteConnectionStringBuilder(SqliteDatabase.BuildConnectionString("eagleeye.db"));

        Assert.Equal(("eagleeye.db", false), (builder.DataSource, builder.Pooling));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildConnectionString_MissingPath_ThrowsArgumentException(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => SqliteDatabase.BuildConnectionString(path!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_MissingConnectionString_ThrowsArgumentException(string? connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestDatabase(connectionString!, []));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed class TestDatabase : SqliteDatabase
    {
        private readonly string[] _migrations;

        public static TestDatabase InMemory(params string[] migrations) => new(InMemoryConnection, migrations);

        public TestDatabase(string connectionString, params string[] migrations)
            : base(connectionString)
        {
            _migrations = migrations;
        }

        protected override IReadOnlyList<string> Migrations => _migrations;

        public Task<long> CountColumnsAsync(string table)
        {
            return ExecuteAsync((connection, _) => ScalarAsync(connection, $"SELECT COUNT(*) FROM pragma_table_info('{table}');"));
        }
    }
}
