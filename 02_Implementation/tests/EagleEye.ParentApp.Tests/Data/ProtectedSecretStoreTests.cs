using System.Text;
using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.ParentApp.Tests.Data;

public sealed class ProtectedSecretStoreTests : IAsyncLifetime
{
    private readonly ParentDatabase _database = new("Data Source=:memory:");
    private readonly ProtectedSecretStore _store;

    public ProtectedSecretStoreTests()
    {
        _store = new ProtectedSecretStore(_database, new XorProtector());
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task GetAsync_Absent_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("pairing-token"));
    }

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsValue()
    {
        await _store.SetAsync("pairing-token", "secret-ä");

        Assert.Equal("secret-ä", await _store.GetAsync("pairing-token"));
    }

    [Fact]
    public async Task SetAsync_StoresProtectedBytesOnly()
    {
        await _store.SetAsync("pairing-token", "secret");

        var stored = await _database.ExecuteAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ProtectedValue FROM Secrets;";
            return (byte[])(await command.ExecuteScalarAsync(ct))!;
        });
        Assert.NotEqual(Encoding.UTF8.GetBytes("secret"), stored);
    }

    [Fact]
    public async Task SetAsync_Twice_OverwritesValue()
    {
        await _store.SetAsync("pairing-token", "one");

        await _store.SetAsync("pairing-token", "two");

        Assert.Equal("two", await _store.GetAsync("pairing-token"));
    }

    [Fact]
    public async Task RemoveAsync_RemovesValue()
    {
        await _store.SetAsync("pairing-token", "secret");

        await _store.RemoveAsync("pairing-token");

        Assert.Null(await _store.GetAsync("pairing-token"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Methods_MissingKey_ThrowArgumentException(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.GetAsync(key!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.SetAsync(key!, "v"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.RemoveAsync(key!));
    }

    [Fact]
    public async Task GetAsync_DatabaseBusy_WaitsForExclusiveAccess()
    {
        await _store.SetAsync("k", "v");

        var value = await PairingStoreTests.WhileDatabaseBusyAsync(_database, () => _store.GetAsync("k"));

        Assert.Equal("v", value);
    }

    [Fact]
    public async Task Methods_DatabaseError_Propagate()
    {
        await PairingStoreTests.DropTableAsync(_database, "Secrets");

        await Assert.ThrowsAsync<SqliteException>(() => _store.GetAsync("k"));
        await Assert.ThrowsAsync<SqliteException>(() => _store.SetAsync("k", "v"));
        await Assert.ThrowsAsync<SqliteException>(() => _store.RemoveAsync("k"));
    }

    [Fact]
    public async Task SetAsync_NullValue_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SetAsync("k", null!));
    }
}
