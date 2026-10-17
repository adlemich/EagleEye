using EagleEye.Service.Data;
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
        await _repository.SaveAsync(new StoredWarningSettings(5, null, 1, "Schnell \U0001F680"), Now);
        await _repository.SaveAsync(new StoredWarningSettings(3, "Bald\nSchluss", 2, null), Now);

        Assert.Equal(new StoredWarningSettings(3, "Bald\nSchluss", 2, null), await _repository.LoadAsync());
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    [InlineData(60, 1)]
    [InlineData(5, 0)]
    public async Task Save_InvalidLevels_RejectedBySchema(int early, int final)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _repository.SaveAsync(new StoredWarningSettings(early, null, final, null), Now));
    }

    [Fact]
    public async Task Save_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!, Now));
    }
}
