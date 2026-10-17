using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Settings;

/// <summary>
/// Client side of the global state area "WarningSettings" (US-006, FR-APP-054). Every write returns <c>true</c> when a
/// snapshot confirming it was applied, and <c>false</c> on rejection, connection loss or timeout.
/// </summary>
public interface IWarningSettingsModel
{
    /// <summary>Raised (on any thread) when the load state or the snapshot changed.</summary>
    event Action? Changed;

    /// <summary>Whether the settings are available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot, or null.</summary>
    WarningSettingsDto? Snapshot { get; }

    /// <summary>Sets the minutes of a level (AC-15).</summary>
    Task<bool> SetMinutesAsync(WarningLevel level, int minutes);

    /// <summary>Sets the text of a level; empty or a default restores the default (AC-15).</summary>
    Task<bool> SetTextAsync(WarningLevel level, string text);
}

/// <summary>
/// Client side of "WarningSettings": fetched on every confirmed (re)connect, broadcasts applied by the revision rule,
/// writes confirmed like the per-account areas (4 s timeout). The global area is the one fixed key of
/// <see cref="SelectedAccountModel{TSnapshot}"/>, so both share the same replica and write logic.
/// </summary>
public sealed class WarningSettingsModel : SelectedAccountModel<WarningSettingsDto>, IWarningSettingsModel
{
    private const string AreaKey = "WarningSettings";

    /// <summary>Creates the model and follows the gateway.</summary>
    public WarningSettingsModel(IParentHubGateway gateway, TimeProvider timeProvider)
        : base(gateway, timeProvider, snapshot => snapshot.Revision)
    {
        gateway.WarningSettingsChanged += OnBroadcast;
        SelectAccount(AreaKey);
    }

    /// <inheritdoc />
    public Task<bool> SetMinutesAsync(WarningLevel level, int minutes) =>
        WriteAsync((client, id, _, ct) => client.SetWarningMinutesAsync(id, level, minutes, ct));

    /// <inheritdoc />
    public Task<bool> SetTextAsync(WarningLevel level, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WriteAsync((client, id, _, ct) => client.SetWarningTextAsync(id, level, text, ct));
    }

    /// <inheritdoc />
    protected override Task<WarningSettingsDto> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct) =>
        client.GetWarningSettingsAsync(ct);

    /// <inheritdoc />
    protected override string AccountSidOf(WarningSettingsDto snapshot) => AreaKey;

    /// <inheritdoc />
    protected override Guid? LastRequestOf(WarningSettingsDto snapshot) => snapshot.LastChangeRequestId;
}
