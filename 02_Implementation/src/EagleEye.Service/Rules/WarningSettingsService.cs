using EagleEye.Service.Data;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.Shared.Resources;

namespace EagleEye.Service.Rules;

/// <summary>State owner of the global area "WarningSettings" (ADR-010 §9, US-006 Decision 1, FR-APP-054).</summary>
public interface IWarningSettingsService
{
    /// <summary>The current settings; read lock-free by the time-limit engine.</summary>
    WarningSettings Current { get; }

    /// <summary>Loads the stored settings (service start).</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>The snapshot.</summary>
    Task<WarningSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Sets the minutes of a level, validated against the stored other level (AC-15).</summary>
    /// <exception cref="RuleValidationException">The early warning would not stay greater than the final warning.</exception>
    Task<StateWriteAckDto> SetMinutesAsync(Guid requestId, WarningLevel level, int minutes, string deviceName, CancellationToken ct = default);

    /// <summary>Sets the text of a level; empty, white space only or a default text of any language restores the default.</summary>
    Task<StateWriteAckDto> SetTextAsync(Guid requestId, WarningLevel level, string text, string deviceName, CancellationToken ct = default);
}

/// <summary>
/// State owner of "WarningSettings": one lock; validate against the stored values → store → replace → revision++ → log →
/// broadcast → ack (same pattern as the account rules). No row = defaults (5 / 1 minutes, default texts).
/// </summary>
public sealed class WarningSettingsService(
    IWarningSettingsRepository repository,
    IAccountRulesBroadcaster broadcaster,
    TimeProvider timeProvider,
    ILogger<WarningSettingsService> logger) : IWarningSettingsService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WarningSettings _current = WarningSettings.Defaults;
    private Guid? _lastRequest;
    private long _revision = 1;

    /// <inheritdoc />
    public WarningSettings Current => Volatile.Read(ref _current);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => LockedAsync(async () =>
    {
        var stored = await repository.LoadAsync(ct).ConfigureAwait(false);
        var settings = stored is null ? WarningSettings.Defaults : new WarningSettings(stored.EarlyMinutes, stored.EarlyText, stored.FinalMinutes, stored.FinalText);
        Volatile.Write(ref _current, settings);
        logger.LogInformation(
            "Warning levels loaded: early {Early} min, final {Final} min.", settings.EarlyMinutes, settings.FinalMinutes);
        return true;
    }, ct);

    /// <inheritdoc />
    public Task<WarningSettingsDto> GetAsync(CancellationToken ct = default) => LockedAsync(() => Task.FromResult(BuildDto()), ct);

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetMinutesAsync(Guid requestId, WarningLevel level, int minutes, string deviceName, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(level) || !WarningRules.IsValidMinutes(minutes))
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Invalid warning level.");
        }

        return WriteAsync(requestId, deviceName, current =>
        {
            var changed = current.WithMinutes(level, minutes);
            return WarningRules.IsValidPair(changed.EarlyMinutes, changed.FinalMinutes)
                ? (changed, AppRuleChangeLog.LevelsChanged(changed.EarlyMinutes, changed.FinalMinutes))
                : throw new RuleValidationException(RuleViolation.FinalNotLessThanEarly);
        }, ct);
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetTextAsync(Guid requestId, WarningLevel level, string text, string deviceName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = BreakTimeRules.NormalizeLineBreaks(text);
        if (!Enum.IsDefined(level) || !BreakTimeRules.IsValidDisplayText(normalized))
        {
            throw new ArgumentException("The warning text is not valid.", nameof(text));
        }

        var kind = level == WarningLevel.Early ? DefaultTextKind.EarlyWarningText : DefaultTextKind.FinalWarningText;
        var custom = DefaultTexts.MeansDefault(kind, normalized) ? null : normalized;
        return WriteAsync(requestId, deviceName, current => (current.WithText(level, custom), AppRuleChangeLog.WarningTextChanged(level, custom is null)), ct);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private Task<StateWriteAckDto> WriteAsync(
        Guid requestId, string deviceName, Func<WarningSettings, (WarningSettings Settings, string Change)> apply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(deviceName);
        return LockedAsync(async () =>
        {
            var (settings, change) = apply(_current);
            await repository.SaveAsync(
                new StoredWarningSettings(settings.EarlyMinutes, settings.EarlyText, settings.FinalMinutes, settings.FinalText), timeProvider.GetUtcNow(), ct)
                .ConfigureAwait(false);
            Volatile.Write(ref _current, settings);
            _revision++;
            _lastRequest = requestId;
            logger.LogInformation(
                "Warning settings changed by {DeviceName}: {Change} (request {RequestId}, revision {Revision}).", deviceName, change, requestId, _revision);
            var snapshot = BuildDto();
            try
            {
                await broadcaster.BroadcastAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // ADR-010 §9: the state is stored; clients that missed the broadcast recover on their next fetch.
                logger.LogWarning(ex, "Broadcasting the warning settings (revision {Revision}) failed.", snapshot.Revision);
            }

            return new StateWriteAckDto(_revision);
        }, ct);
    }

    private WarningSettingsDto BuildDto() =>
        new(_revision, _lastRequest, _current.EarlyMinutes, _current.EarlyText, _current.FinalMinutes, _current.FinalText);

    private async Task<T> LockedAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
