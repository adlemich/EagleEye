using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Service.Pairing;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Pairing;

public sealed class PairingManagerTests
{
    private const string ConnectionA = "connection-a";
    private const string ConnectionB = "connection-b";
    private const string Code = "042137";
    private const string DeviceName = "Dad's laptop";
    private const string Token = "secret-token";

    private static readonly byte[] TokenHash = Enumerable.Repeat((byte)7, 32).ToArray();

    private readonly Mock<IPairingCodeGenerator> _generator = new();
    private readonly Mock<IPairingTokenService> _tokens = new();
    private readonly Mock<IPairedDeviceRepository> _repository = new();
    private readonly Mock<IPairingCodeNotifier> _notifier = new();
    private readonly Mock<ILogger<PairingManager>> _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly PairingManager _manager;

    public PairingManagerTests()
    {
        _generator.Setup(g => g.Generate()).Returns(Code);
        _tokens.Setup(t => t.CreateToken()).Returns(Token);
        _tokens.Setup(t => t.Hash(Token)).Returns(TokenHash);
        _manager = new PairingManager(
            _generator.Object, _tokens.Object, _repository.Object, _notifier.Object, _time, _logger.Object);
    }

    [Fact]
    public async Task StartPairingAsync_NotifiesGeneratedCode()
    {
        await _manager.StartPairingAsync(ConnectionA);

        _notifier.Verify(n => n.NotifyAsync(Code), Times.Once);
    }

    [Fact]
    public async Task StartPairingAsync_SecondStart_ReplacesFirstCode()
    {
        await _manager.StartPairingAsync(ConnectionA);
        _generator.Setup(g => g.Generate()).Returns("999999");
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.WrongCode, result.Outcome);
    }

    [Fact]
    public async Task StartPairingAsync_SecondStartFromOtherConnection_FirstConnectionHasNoCode()
    {
        await _manager.StartPairingAsync(ConnectionA);
        await _manager.StartPairingAsync(ConnectionB);

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.NoPendingCode, result.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_CorrectCode_ReturnsSuccessWithDeviceIdAndToken()
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.Success, result.Outcome);
        Assert.True(Guid.TryParse(result.DeviceId, out _));
        Assert.Equal(Token, result.Token);
    }

    [Fact]
    public async Task SubmitAsync_CorrectCode_StoresHashNotToken()
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, Code, "  " + DeviceName + " ");

        _repository.Verify(r => r.AddAsync(
            It.Is<PairedDevice>(d =>
                d.DeviceId == result.DeviceId
                && d.DeviceName == DeviceName
                && d.TokenHash.SequenceEqual(TokenHash)
                && d.PairedAtUtc == _time.GetUtcNow()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_CorrectCodeTwice_SecondHasNoPendingCode()
    {
        await _manager.StartPairingAsync(ConnectionA);
        await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.NoPendingCode, result.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_WrongCode_ReturnsWrongCode()
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, "000000", DeviceName);

        Assert.Equal(new PairingResultDto(PairingOutcome.WrongCode, null, null), result);
    }

    [Fact]
    public async Task SubmitAsync_ExactlyAtExpiry_Succeeds()
    {
        await _manager.StartPairingAsync(ConnectionA);
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_AfterExpiry_ReturnsCodeExpired()
    {
        await _manager.StartPairingAsync(ConnectionA);
        _time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.CodeExpired, result.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_NoPendingCode_ReturnsNoPendingCode()
    {
        var result = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.NoPendingCode, result.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_OtherConnection_ReturnsNoPendingCode()
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionB, Code, DeviceName);

        Assert.Equal(PairingOutcome.NoPendingCode, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task SubmitAsync_InvalidFormat_ReturnsInvalidCodeFormat(string? code)
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, code, DeviceName);

        Assert.Equal(PairingOutcome.InvalidCodeFormat, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("this-name-is-much-longer-than-fifty-characters-in-total")]
    public async Task SubmitAsync_InvalidName_ReturnsInvalidDeviceName(string? name)
    {
        await _manager.StartPairingAsync(ConnectionA);

        var result = await _manager.SubmitAsync(ConnectionA, Code, name);

        Assert.Equal(PairingOutcome.InvalidDeviceName, result.Outcome);
    }

    [Theory]
    [MemberData(nameof(FailingSubmissions))]
    public async Task SubmitAsync_AnyFailure_ClearsPendingCode(string connectionId, string? code, string? name, TimeSpan delay)
    {
        await _manager.StartPairingAsync(ConnectionA);
        _time.Advance(delay);
        await _manager.SubmitAsync(connectionId, code, name);

        var retry = await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        Assert.Equal(PairingOutcome.NoPendingCode, retry.Outcome);
    }

    [Fact]
    public async Task SubmitAsync_Failure_DoesNotStoreDevice()
    {
        await _manager.StartPairingAsync(ConnectionA);

        await _manager.SubmitAsync(ConnectionA, "111111", DeviceName);

        _repository.Verify(r => r.AddAsync(It.IsAny<PairedDevice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_AnyOutcome_NeverLogsCodeOrToken()
    {
        await _manager.StartPairingAsync(ConnectionA);
        await _manager.SubmitAsync(ConnectionA, "111111", DeviceName);
        await _manager.StartPairingAsync(ConnectionA);
        await _manager.SubmitAsync(ConnectionA, Code, DeviceName);

        _logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    $"{state}".Contains(Code) || $"{state}".Contains("111111") || $"{state}".Contains(Token)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_KnownToken_ReturnsDevice()
    {
        var device = new PairedDevice("id", DeviceName, TokenHash, DateTimeOffset.UnixEpoch);
        _repository.Setup(r => r.FindByTokenHashAsync(TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        var result = await _manager.AuthenticateAsync(Token);

        Assert.Same(device, result);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownToken_ReturnsNull()
    {
        _tokens.Setup(t => t.Hash("unknown")).Returns(new byte[32]);

        var result = await _manager.AuthenticateAsync("unknown");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AuthenticateAsync_EmptyToken_ReturnsNullWithoutLookup(string? token)
    {
        var result = await _manager.AuthenticateAsync(token);

        Assert.Null(result);
        _repository.Verify(r => r.FindByTokenHashAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoveDeviceAsync_ReturnsRepositoryResult(bool existed)
    {
        _repository.Setup(r => r.RemoveAsync("id", It.IsAny<CancellationToken>())).ReturnsAsync(existed);

        var result = await _manager.RemoveDeviceAsync("id");

        Assert.Equal(existed, result);
    }

    [Fact]
    public async Task StartPairingAsync_EmptyConnectionId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _manager.StartPairingAsync(""));
    }

    [Fact]
    public async Task SubmitAsync_EmptyConnectionId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _manager.SubmitAsync(" ", Code, DeviceName));
    }

    [Fact]
    public async Task RemoveDeviceAsync_EmptyDeviceId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _manager.RemoveDeviceAsync(""));
    }

    public static TheoryData<string, string?, string?, TimeSpan> FailingSubmissions => new()
    {
        { ConnectionA, "000000", DeviceName, TimeSpan.Zero },
        { ConnectionA, Code, DeviceName, TimeSpan.FromMinutes(6) },
        { ConnectionB, Code, DeviceName, TimeSpan.Zero },
        { ConnectionA, "12x456", DeviceName, TimeSpan.Zero },
        { ConnectionA, Code, "", TimeSpan.Zero },
    };
}
