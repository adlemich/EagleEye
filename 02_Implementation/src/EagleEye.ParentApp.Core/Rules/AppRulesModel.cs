using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of the state area "AppRules:{sid}" (US-006, ADR-010) for the account selected on the Rules page. Every
/// write returns <c>true</c> when a snapshot confirming it was applied, and <c>false</c> on rejection, connection loss
/// or timeout (then the stored state is fetched again if the outcome is unknown).
/// </summary>
public interface IAppRulesModel
{
    /// <summary>Raised (on any thread) when the selection, the load state or the snapshot changed.</summary>
    event Action? Changed;

    /// <summary>The selected account's SID, or null.</summary>
    string? SelectedAccountSid { get; }

    /// <summary>Whether the app rules of the selected account are available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot of the selected account, or null.</summary>
    AppRulesDto? Snapshot { get; }

    /// <summary>Selects an account (or none) and fetches its app rules while connected (AC-1).</summary>
    void SelectAccount(string? accountSid);

    /// <summary>Sets the hours or the minutes of an app's budget (AC-7, AC-8).</summary>
    Task<bool> SetBudgetAsync(long appId, AppBudgetPart part, int value);

    /// <summary>Ticks or unticks a weekday of an app (AC-7).</summary>
    Task<bool> SetDayAsync(long appId, DayOfWeek day, bool isAllowed);

    /// <summary>Sets the budget text; empty or a default restores the default (AC-10).</summary>
    Task<bool> SetBudgetTextAsync(string text);
}

/// <summary>
/// Client side of "AppRules:{sid}" (US-006 Step 8): fetch, revision rule, write confirmation and the 4 s write timeout of
/// <see cref="SelectedAccountModel{TSnapshot}"/>.
/// </summary>
public sealed class AppRulesModel : SelectedAccountModel<AppRulesDto>, IAppRulesModel
{
    /// <summary>Creates the model and follows the gateway.</summary>
    public AppRulesModel(IParentHubGateway gateway, TimeProvider timeProvider)
        : base(gateway, timeProvider, snapshot => snapshot.Revision)
    {
        gateway.AppRulesChanged += OnBroadcast;
    }

    /// <inheritdoc />
    public Task<bool> SetBudgetAsync(long appId, AppBudgetPart part, int value) =>
        WriteAsync((client, id, sid, ct) => client.SetAppBudgetAsync(id, sid, appId, part, value, ct));

    /// <inheritdoc />
    public Task<bool> SetDayAsync(long appId, DayOfWeek day, bool isAllowed) =>
        WriteAsync((client, id, sid, ct) => client.SetAppDayAsync(id, sid, appId, day, isAllowed, ct));

    /// <inheritdoc />
    public Task<bool> SetBudgetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WriteAsync((client, id, sid, ct) => client.SetBudgetTextAsync(id, sid, text, ct));
    }

    /// <inheritdoc />
    protected override Task<AppRulesDto> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct) =>
        client.GetAppRulesAsync(accountSid, ct);

    /// <inheritdoc />
    protected override string AccountSidOf(AppRulesDto snapshot) => snapshot.AccountSid;

    /// <inheritdoc />
    protected override Guid? LastRequestOf(AppRulesDto snapshot) => snapshot.LastChangeRequestId;
}
