using EagleEye.Service.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class PairedDeviceRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset PairedAt = new(2026, 10, 4, 12, 30, 15, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly PairedDeviceRepository _repository;

    public PairedDeviceRepositoryTests()
    {
        _repository = new PairedDeviceRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task InitializeAsync_NewDatabase_IsAtSchemaVersion1()
    {
        Assert.Equal(1, await _database.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task FindByTokenHashAsync_AddedDevice_ReturnsIt()
    {
        var device = CreateDevice("device-1", 1);
        await _repository.AddAsync(device);

        var found = await _repository.FindByTokenHashAsync(Hash(1));

        Assert.NotNull(found);
        Assert.Equal((device.DeviceId, device.DeviceName, device.PairedAtUtc), (found.DeviceId, found.DeviceName, found.PairedAtUtc));
        Assert.Equal(device.TokenHash, found.TokenHash);
    }

    [Fact]
    public async Task FindByTokenHashAsync_UnknownHash_ReturnsNull()
    {
        await _repository.AddAsync(CreateDevice("device-1", 1));

        var found = await _repository.FindByTokenHashAsync(Hash(2));

        Assert.Null(found);
    }

    [Fact]
    public async Task RemoveAsync_ExistingDevice_ReturnsTrueAndRemovesIt()
    {
        await _repository.AddAsync(CreateDevice("device-1", 1));

        var removed = await _repository.RemoveAsync("device-1");

        Assert.True(removed);
        Assert.Null(await _repository.FindByTokenHashAsync(Hash(1)));
    }

    [Fact]
    public async Task RemoveAsync_UnknownDevice_ReturnsFalse()
    {
        Assert.False(await _repository.RemoveAsync("device-9"));
    }

    [Fact]
    public async Task RemoveAsync_OneOfTwo_KeepsTheOther()
    {
        await _repository.AddAsync(CreateDevice("device-1", 1));
        await _repository.AddAsync(CreateDevice("device-2", 2));

        await _repository.RemoveAsync("device-1");

        Assert.NotNull(await _repository.FindByTokenHashAsync(Hash(2)));
    }

    [Fact]
    public async Task AddAsync_DuplicateTokenHash_ThrowsSqliteException()
    {
        await _repository.AddAsync(CreateDevice("device-1", 1));

        await Assert.ThrowsAsync<SqliteException>(() => _repository.AddAsync(CreateDevice("device-2", 1)));
    }

    [Fact]
    public async Task AddAsync_NullDevice_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.AddAsync(null!));
    }

    [Fact]
    public async Task FindByTokenHashAsync_NullHash_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.FindByTokenHashAsync(null!));
    }

    [Fact]
    public async Task RemoveAsync_EmptyId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.RemoveAsync(""));
    }

    private static PairedDevice CreateDevice(string deviceId, byte hashSeed)
    {
        return new PairedDevice(deviceId, "Name " + deviceId, Hash(hashSeed), PairedAt);
    }

    private static byte[] Hash(byte seed)
    {
        return Enumerable.Repeat(seed, 32).ToArray();
    }
}
