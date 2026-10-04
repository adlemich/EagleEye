using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.ParentApp.Tests.Data;

public sealed class PairingStoreTests : IAsyncLifetime
{
    private static readonly StoredPairing Pairing = new(
        "kid-pc", "device-1", "Dad's laptop", "AB12", new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), "token-1");

    private readonly ParentDatabase _database = new("Data Source=:memory:");
    private readonly InMemorySecretStore _secrets = new();
    private readonly PairingStore _store;

    public PairingStoreTests()
    {
        _store = new PairingStore(_database, _secrets);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LoadAsync_Empty_ReturnsNull()
    {
        Assert.Null(await _store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_AfterSave_ReturnsPairingIncludingToken()
    {
        await _store.SaveAsync(Pairing);

        Assert.Equal(Pairing, await _store.LoadAsync());
    }

    [Fact]
    public async Task SaveAsync_StoresTokenInSecretStoreNotInDatabase()
    {
        await _store.SaveAsync(Pairing);

        var columnsWithToken = await _database.ExecuteAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ServerConnections WHERE Host LIKE '%token-1%' OR DeviceId LIKE '%token-1%' OR DeviceName LIKE '%token-1%' OR CertificateThumbprint LIKE '%token-1%';";
            return (long)(await command.ExecuteScalarAsync(ct))!;
        });
        Assert.Equal(("token-1", 0L), (_secrets.Values[PairingStore.TokenKey], columnsWithToken));
    }

    [Fact]
    public async Task SaveAsync_Twice_KeepsOnlyLatestPairing()
    {
        await _store.SaveAsync(Pairing);
        var second = Pairing with { Host = "other-pc", Token = "token-2" };

        await _store.SaveAsync(second);

        Assert.Equal(second, await _store.LoadAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesPairingAndToken()
    {
        await _store.SaveAsync(Pairing);

        await _store.DeleteAsync();

        Assert.Null(await _store.LoadAsync());
        Assert.Empty(_secrets.Values);
    }

    [Fact]
    public async Task LoadAsync_RowWithoutToken_ReturnsNull()
    {
        await _store.SaveAsync(Pairing);
        _secrets.Values.Clear();

        Assert.Null(await _store.LoadAsync());
    }

    [Fact]
    public async Task SaveAsync_NullPairing_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
    }

    [Fact]
    public async Task Methods_DatabaseError_Propagate()
    {
        await DropTableAsync(_database, "ServerConnections");

        await Assert.ThrowsAsync<SqliteException>(() => _store.LoadAsync());
        await Assert.ThrowsAsync<SqliteException>(() => _store.SaveAsync(Pairing));
        await Assert.ThrowsAsync<SqliteException>(() => _store.DeleteAsync());
    }

    [Fact]
    public async Task Methods_DatabaseBusy_WaitForExclusiveAccess()
    {
        await WhileDatabaseBusyAsync(_database, () => _store.SaveAsync(Pairing));
        var loaded = await WhileDatabaseBusyAsync(_database, () => _store.LoadAsync());
        await WhileDatabaseBusyAsync(_database, () => _store.DeleteAsync());

        Assert.Equal(Pairing, loaded);
    }

    internal static async Task WhileDatabaseBusyAsync(ParentDatabase database, Func<Task> action)
    {
        var release = new TaskCompletionSource();
        var busy = database.ExecuteAsync(async (_, _) =>
        {
            await release.Task;
            return 0;
        });
        var task = action();
        Assert.False(task.IsCompleted);
        release.SetResult();
        await busy;
        await task;
    }

    internal static async Task<T> WhileDatabaseBusyAsync<T>(ParentDatabase database, Func<Task<T>> action)
    {
        Task<T>? task = null;
        await WhileDatabaseBusyAsync(database, (Func<Task>)(() => task = action()));
        return await task!;
    }

    internal static Task DropTableAsync(ParentDatabase database, string table)
    {
        return database.ExecuteAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE {table};";
            return await command.ExecuteNonQueryAsync(ct);
        });
    }

    [Fact]
    public void StoredPairing_ToString_DoesNotContainToken()
    {
        Assert.DoesNotContain("token-1", Pairing.ToString());
    }

    [Fact]
    public async Task ParentDatabase_Initialize_IsAtSchemaVersion1()
    {
        Assert.Equal(1, await _database.GetSchemaVersionAsync());
    }
}
