using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of the state area "AppGroupRules:{sid}" (US-008, ADR-010) for the account selected on the Rules page. The
/// ids of <see cref="IRuleTableWrites"/> are group ids (AC-16).
/// </summary>
public interface IAppGroupRulesModel : IRuleTableWrites
{
    /// <summary>Raised (on any thread) when the selection, the load state or the snapshot changed.</summary>
    event Action? Changed;

    /// <summary>Whether the group rules of the selected account are available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot of the selected account, or null.</summary>
    AppGroupRulesDto? Snapshot { get; }

    /// <summary>Selects an account (or none) and fetches its group rules while connected (AC-15).</summary>
    void SelectAccount(string? accountSid);
}

/// <summary>
/// Client side of "AppGroupRules:{sid}" (US-008 Step 8): fetch, revision rule, write confirmation and the 4 s write timeout
/// of <see cref="SelectedAccountModel{TSnapshot}"/>.
/// </summary>
public sealed class AppGroupRulesModel : SelectedAccountModel<AppGroupRulesDto>, IAppGroupRulesModel
{
    /// <summary>Creates the model and follows the gateway.</summary>
    public AppGroupRulesModel(IParentHubGateway gateway, TimeProvider timeProvider)
        : base(gateway, timeProvider, snapshot => snapshot.Revision)
    {
        gateway.AppGroupRulesChanged += OnBroadcast;
    }

    /// <inheritdoc />
    public Task<bool> SetBudgetAsync(long id, AppBudgetPart part, int value) =>
        WriteAsync((client, requestId, sid, ct) => client.SetAppGroupBudgetAsync(requestId, sid, id, part, value, ct));

    /// <inheritdoc />
    public Task<bool> SetDayAsync(long id, DayOfWeek day, bool isAllowed) =>
        WriteAsync((client, requestId, sid, ct) => client.SetAppGroupDayAsync(requestId, sid, id, day, isAllowed, ct));

    /// <inheritdoc />
    protected override Task<AppGroupRulesDto> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct) =>
        client.GetAppGroupRulesAsync(accountSid, ct);

    /// <inheritdoc />
    protected override string AccountSidOf(AppGroupRulesDto snapshot) => snapshot.AccountSid;

    /// <inheritdoc />
    protected override Guid? LastRequestOf(AppGroupRulesDto snapshot) => snapshot.LastChangeRequestId;
}
