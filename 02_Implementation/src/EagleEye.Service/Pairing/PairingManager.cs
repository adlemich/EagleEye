using System.Security.Cryptography;
using System.Text;
using EagleEye.Service.Communication;
using EagleEye.Service.Data;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Pairing;

/// <summary>
/// Holds the single pending pairing code <c>{code, connectionId, expiresAt}</c> and validates
/// submissions (ADR-008 §4). Any submission clears the pending code, so every code allows one
/// guess. Codes and tokens are never logged.
/// </summary>
public sealed class PairingManager(
    IPairingCodeGenerator codeGenerator,
    IPairingTokenService tokenService,
    IPairedDeviceRepository repository,
    IPairingCodeNotifier notifier,
    TimeProvider timeProvider,
    ILogger<PairingManager> logger) : IPairingManager
{
    private readonly Lock _lock = new();
    private PendingCode? _pending;

    /// <inheritdoc />
    public async Task StartPairingAsync(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var code = codeGenerator.Generate();
        lock (_lock)
        {
            _pending = new PendingCode(code, connectionId, timeProvider.GetUtcNow() + PairingRules.CodeLifetime);
        }

        logger.LogInformation("Pairing started by connection {ConnectionId}; code ***.", connectionId);
        await notifier.NotifyAsync(code).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PairingResultDto> SubmitAsync(string connectionId, string? code, string? deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        PendingCode? pending;
        lock (_lock)
        {
            pending = _pending;
            _pending = null;
        }

        var name = PairingRules.NormalizeDeviceName(deviceName);
        var outcome = Validate(connectionId, code, name, pending);
        if (outcome != PairingOutcome.Success)
        {
            logger.LogWarning("Pairing rejected for connection {ConnectionId}: {Outcome}.", connectionId, outcome);
            return new PairingResultDto(outcome, null, null);
        }

        // Validate() returns Success only with a valid name.
        return await PairAsync(connectionId, name!).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PairedDevice?> AuthenticateAsync(string? token)
    {
        return string.IsNullOrEmpty(token)
            ? Task.FromResult<PairedDevice?>(null)
            : repository.FindByTokenHashAsync(tokenService.Hash(token));
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDeviceAsync(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        var removed = await repository.RemoveAsync(deviceId).ConfigureAwait(false);
        logger.LogInformation("Remove parent device {DeviceId}: {Result}.", deviceId, removed ? "removed" : "unknown");
        return removed;
    }

    private PairingOutcome Validate(string connectionId, string? code, string? name, PendingCode? pending)
    {
        if (!PairingRules.IsValidCodeFormat(code))
        {
            return PairingOutcome.InvalidCodeFormat;
        }

        if (name is null)
        {
            return PairingOutcome.InvalidDeviceName;
        }

        if (pending is null || pending.ConnectionId != connectionId)
        {
            return PairingOutcome.NoPendingCode;
        }

        if (timeProvider.GetUtcNow() > pending.ExpiresAt)
        {
            return PairingOutcome.CodeExpired;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code!), Encoding.ASCII.GetBytes(pending.Code))
            ? PairingOutcome.Success
            : PairingOutcome.WrongCode;
    }

    private async Task<PairingResultDto> PairAsync(string connectionId, string deviceName)
    {
        var deviceId = Guid.NewGuid().ToString("D");
        var token = tokenService.CreateToken();
        var device = new PairedDevice(deviceId, deviceName, tokenService.Hash(token), timeProvider.GetUtcNow());
        await repository.AddAsync(device).ConfigureAwait(false);

        logger.LogInformation(
            "Parent device {DeviceId} ({DeviceName}) paired by connection {ConnectionId}.", deviceId, deviceName, connectionId);
        return new PairingResultDto(PairingOutcome.Success, deviceId, token);
    }

    private sealed record PendingCode(string Code, string ConnectionId, DateTimeOffset ExpiresAt);
}
