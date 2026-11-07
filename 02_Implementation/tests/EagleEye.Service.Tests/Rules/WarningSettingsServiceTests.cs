using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class WarningSettingsServiceTests : IAsyncLifetime
{
    private const string Device = "Dad's laptop";
    private static readonly Guid RequestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly WarningSettingsRepository _repository;
    private readonly List<WarningSettingsDto> _broadcasts = [];
    private readonly Mock<IAccountRulesBroadcaster> _broadcaster = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
    private readonly TestLogger<WarningSettingsService> _logger = new();
    private readonly WarningSettingsService _service;

    public WarningSettingsServiceTests()
    {
        _repository = new WarningSettingsRepository(_database);
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<WarningSettingsDto>())).Callback<WarningSettingsDto>(_broadcasts.Add).Returns(Task.CompletedTask);
        _service = new WarningSettingsService(_repository, _broadcaster.Object, _time, _logger);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _service.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Defaults_WithoutRow()
    {
        Assert.Equal(WarningSettings.Defaults, _service.Current);
        Assert.Equal(new WarningSettingsDto(1, null, 5, 1), await _service.GetAsync());
        Assert.Contains("Warning levels loaded: early 5 min, final 1 min.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task Initialize_StoredRow_Loaded()
    {
        await _repository.SaveAsync(new StoredWarningSettings(10, 3), _time.GetUtcNow());
        using var service = new WarningSettingsService(_repository, _broadcaster.Object, _time, _logger);

        await service.InitializeAsync();

        Assert.Equal(new WarningSettings(10, 3), service.Current);
    }

    [Fact]
    public async Task SetMinutesAsync_ValidAgainstStoredOther_StoredBroadcastLogged()
    {
        await _service.SetMinutesAsync(RequestA, WarningLevel.Final, 2, Device);
        var ack = await _service.SetMinutesAsync(RequestA, WarningLevel.Early, 3, Device);

        Assert.Equal(3, ack.Revision);
        Assert.Equal((3, 2), (_service.Current.EarlyMinutes, _service.Current.FinalMinutes));
        Assert.Equal(new StoredWarningSettings(3, 2), await _repository.LoadAsync());
        Assert.Equal(new WarningSettingsDto(3, RequestA, 3, 2), _broadcasts[^1]);
        Assert.Equal(
            "Warning settings changed by Dad's laptop: warning levels changed: early 3 min, final 2 min (request aaaaaaaa-0000-0000-0000-000000000001, revision 3).",
            _logger.Messages(LogLevel.Information)[^1]);
    }

    [Theory]
    [InlineData(WarningLevel.Final, 5)]
    [InlineData(WarningLevel.Final, 6)]
    [InlineData(WarningLevel.Early, 1)]
    public async Task SetMinutesAsync_EarlyNotGreaterThanFinal_RejectedNothingChanged(WarningLevel level, int minutes)
    {
        var ex = await Assert.ThrowsAsync<RuleValidationException>(() => _service.SetMinutesAsync(RequestA, level, minutes, Device));

        Assert.Equal(RuleViolation.FinalNotLessThanEarly, ex.Violation);
        Assert.Equal(WarningSettings.Defaults, _service.Current);
        Assert.Empty(_broadcasts);
    }

    [Theory]
    [InlineData(WarningLevel.Early, 0)]
    [InlineData(WarningLevel.Early, 60)]
    [InlineData((WarningLevel)5, 3)]
    public async Task SetMinutesAsync_OutOfRange_Throws(WarningLevel level, int minutes)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SetMinutesAsync(RequestA, level, minutes, Device));
    }

    [Fact]
    public async Task StorageFails_NothingChanged()
    {
        var repository = new Mock<IWarningSettingsRepository>();
        repository.Setup(r => r.SaveAsync(It.IsAny<StoredWarningSettings>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SqliteException("disk full", 13));
        using var service = new WarningSettingsService(repository.Object, _broadcaster.Object, _time, _logger);

        await Assert.ThrowsAsync<SqliteException>(() => service.SetMinutesAsync(RequestA, WarningLevel.Early, 9, Device));

        Assert.Equal((WarningSettings.Defaults, 1L), (service.Current, (await service.GetAsync()).Revision));
    }

    [Fact]
    public async Task BroadcastFails_WarningLoggedAndAcknowledged()
    {
        var failure = new InvalidOperationException("hub");
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<WarningSettingsDto>())).ThrowsAsync(failure);

        var ack = await _service.SetMinutesAsync(RequestA, WarningLevel.Early, 9, Device);

        Assert.Equal((2L, true), (ack.Revision, _logger.Has(LogLevel.Warning, failure)));
    }

    [Fact]
    public void WarningSettings_Accessors()
    {
        var settings = new WarningSettings(5, 1);

        Assert.Equal((5, 1), (settings.MinutesOf(WarningLevel.Early), settings.MinutesOf(WarningLevel.Final)));
        Assert.Equal(new WarningSettings(7, 1), settings.WithMinutes(WarningLevel.Early, 7));
        Assert.Equal(new WarningSettings(5, 2), settings.WithMinutes(WarningLevel.Final, 2));
    }

    [Fact]
    public void Service_HasNoTextApi()
    {
        Assert.Null(typeof(IWarningSettingsService).GetMethod("SetTextAsync"));
        Assert.Equal(["EarlyMinutes", "FinalMinutes"], typeof(WarningSettings).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(p => p.Name).Where(n => n != "EqualityContract").Order());
    }
}
