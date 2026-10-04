using Microsoft.Data.Sqlite;

namespace EagleEye.Shared.Data;

/// <summary>
/// Base class for the SQLite databases of the service and the parent app (coding guidelines §8,
/// §10.2). Holds one long-lived connection, applies the standard pragmas, runs
/// <c>PRAGMA integrity_check</c> and applies the ordered, forward-only migrations of the derived
/// class, each in its own transaction. Access to the connection is serialized, because a
/// <see cref="SqliteConnection"/> must not be used by two threads at the same time.
/// </summary>
public abstract class SqliteDatabase : IAsyncDisposable
{
    private const string PragmaSql = """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA busy_timeout = 5000;
        PRAGMA foreign_keys = ON;
        """;

    private const string IntegrityOk = "ok";

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    /// <summary>Creates the database object. Call <see cref="InitializeAsync"/> before use.</summary>
    /// <param name="connectionString">SQLite connection string, see <see cref="BuildConnectionString"/>.</param>
    protected SqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connection = new SqliteConnection(connectionString);
    }

    /// <summary>
    /// The schema migrations, in order. Migration <c>n</c> (1-based) brings the schema to version
    /// <c>n</c>. Existing entries must never change; new versions are appended.
    /// </summary>
    protected abstract IReadOnlyList<string> Migrations { get; }

    /// <summary>Builds the connection string for a database file (no connection pooling).</summary>
    public static string BuildConnectionString(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new SqliteConnectionStringBuilder { DataSource = filePath, Pooling = false }.ToString();
    }

    /// <summary>
    /// Opens the connection, applies the pragmas, checks integrity and applies pending migrations.
    /// </summary>
    /// <exception cref="InvalidDataException">The integrity check failed.</exception>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct).ConfigureAwait(false);
        await ExecuteNonQueryAsync(PragmaSql, transaction: null, ct).ConfigureAwait(false);

        var integrity = await ExecuteScalarAsync("PRAGMA integrity_check;", ct).ConfigureAwait(false);
        EnsureIntegrity(integrity as string);

        await MigrateAsync(ct).ConfigureAwait(false);
        _initialized = true;
    }

    /// <summary>Returns the schema version stored in the database.</summary>
    public Task<long> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        return ExecuteAsync((_, token) => ReadSchemaVersionAsync(token), ct);
    }

    /// <summary>Runs <paramref name="work"/> with exclusive access to the open connection.</summary>
    /// <exception cref="InvalidOperationException">The database is not initialized.</exception>
    public async Task<T> ExecuteAsync<T>(Func<SqliteConnection, CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!_initialized)
        {
            throw new InvalidOperationException("The database is not initialized.");
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work(_connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Throws if the result of <c>PRAGMA integrity_check</c> is not "ok".</summary>
    internal static void EnsureIntegrity(string? result)
    {
        if (!string.Equals(result, IntegrityOk, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"SQLite integrity check failed: {result}");
        }
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        await ExecuteNonQueryAsync(
            "CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER NOT NULL);", transaction: null, ct)
            .ConfigureAwait(false);

        var migrations = Migrations;
        for (var version = await ReadSchemaVersionAsync(ct).ConfigureAwait(false); version < migrations.Count; version++)
        {
            await ApplyMigrationAsync(migrations[(int)version], version + 1, ct).ConfigureAwait(false);
        }
    }

    private async Task ApplyMigrationAsync(string sql, long newVersion, CancellationToken ct)
    {
        await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await ExecuteNonQueryAsync(sql, transaction, ct).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                $"DELETE FROM SchemaVersion; INSERT INTO SchemaVersion (Version) VALUES ({newVersion});", transaction, ct)
                .ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<long> ReadSchemaVersionAsync(CancellationToken ct)
    {
        var value = await ExecuteScalarAsync("SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;", ct)
            .ConfigureAwait(false);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task ExecuteNonQueryAsync(string sql, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<object?> ExecuteScalarAsync(string sql, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
}
