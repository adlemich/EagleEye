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
        Assert.Equal(new WarningSettingsDto(1, null, 5, null, 1, null), await _service.GetAsync());
        Assert.Contains("Warning levels loaded: early 5 min, final 1 min.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task Initialize_StoredRow_Loaded()
    {
        await _repository.SaveAsync(new StoredWarningSettings(10, "Bald", 3, null), _time.GetUtcNow());
        using var service = new WarningSettingsService(_repository, _broadcaster.Object, _time, _logger);

        await service.InitializeAsync();

        Assert.Equal(new WarningSettings(10, "Bald", 3, null), service.Current);
    }

    [Fact]
    public async Task SetMinutesAsync_ValidAgainstStoredOther_StoredBroadcastLogged()
    {
        await _service.SetMinutesAsync(RequestA, WarningLevel.Final, 2, Device);
        var ack = await _service.SetMinutesAsync(RequestA, WarningLevel.Early, 3, Device);

        Assert.Equal(3, ack.Revision);
        Assert.Equal((3, 2), (_service.Current.EarlyMinutes, _service.Current.FinalMinutes));
        Assert.Equal(new StoredWarningSettings(3, null, 2, null), await _repository.LoadAsync());
        Assert.Equal(new WarningSettingsDto(3, RequestA, 3, null, 2, null), _broadcasts[^1]);
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
    public async Task SetTextAsync_CustomThenDefault()
    {
        await _service.SetTextAsync(RequestA, WarningLevel.Early, "Bald\r\nSchluss \U0001F680", Device);
        Assert.Equal("Bald\nSchluss \U0001F680", _service.Current.EarlyText);
        Assert.EndsWith("early warning text changed (request aaaaaaaa-0000-0000-0000-000000000001, revision 2).", _logger.Messages(LogLevel.Information)[^1], StringComparison.Ordinal);

        await _service.SetTextAsync(RequestA, WarningLevel.Early, "Attention, you only have 5 minutes left! Save your progress.", Device);

        Assert.Null(_service.Current.EarlyText);
        Assert.Contains("early warning text reset to the default", _logger.Messages(LogLevel.Information)[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!")]
    [InlineData("Attention!!! Only 1 minute left – save quickly now!")]
    public async Task SetTextAsync_Final_EmptyOrAnyDefault_Default(string text)
    {
        await _service.SetTextAsync(RequestA, WarningLevel.Final, "x", Device);

        await _service.SetTextAsync(RequestA, WarningLevel.Final, text, Device);

        Assert.Null(_service.Current.FinalText);
        Assert.Null((await _repository.LoadAsync())!.FinalText);
    }

    [Fact]
    public async Task SetTextAsync_Invalid_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetTextAsync(RequestA, WarningLevel.Early, null!, Device));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetTextAsync(RequestA, WarningLevel.Early, new string('a', 501), Device));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetTextAsync(RequestA, (WarningLevel)5, "x", Device));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetTextAsync(RequestA, WarningLevel.Early, "x", null!));
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
        var settings = new WarningSettings(5, "e", 1, "f");

        Assert.Equal((5, 1, "e", "f"), (settings.MinutesOf(WarningLevel.Early), settings.MinutesOf(WarningLevel.Final), settings.TextOf(WarningLevel.Early), settings.TextOf(WarningLevel.Final)));
        Assert.Equal(new WarningSettings(5, "e", 1, null), settings.WithText(WarningLevel.Final, null));
        Assert.Equal(new WarningSettings(5, null, 1, "f"), settings.WithText(WarningLevel.Early, null));
    }
}
