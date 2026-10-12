using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Client side of the state area "AccountRules:{sid}" (US-005, ADR-010) for the selected account: break times and the
/// display text. Fetch, revision rule, write confirmation and timeouts: <see cref="SelectedAccountModel{TSnapshot}"/>.
/// </summary>
public sealed class AccountRulesModel : SelectedAccountModel<AccountRulesDto>, IAccountRulesModel
{
    /// <summary>Creates the model and follows the gateway.</summary>
    public AccountRulesModel(IParentHubGateway gateway, TimeProvider timeProvider)
        : base(gateway, timeProvider, snapshot => snapshot.Revision)
    {
        gateway.AccountRulesChanged += OnBroadcast;
    }

    /// <inheritdoc />
    public Task<bool> AddEntryAsync() =>
        WriteAsync((client, id, sid, ct) => client.AddBreakTimeEntryAsync(id, sid, ct));

    /// <inheritdoc />
    public Task<bool> DeleteEntryAsync(long entryId) =>
        WriteAsync((client, id, sid, ct) => client.DeleteBreakTimeEntryAsync(id, sid, entryId, ct));

    /// <inheritdoc />
    public Task<bool> SetActiveAsync(long entryId, bool isActive) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryActiveAsync(id, sid, entryId, isActive, ct));

    /// <inheritdoc />
    public Task<bool> SetTimeAsync(long entryId, BreakTimeBoundary boundary, int minute) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryTimeAsync(id, sid, entryId, boundary, minute, ct));

    /// <inheritdoc />
    public Task<bool> SetDayAsync(long entryId, DayOfWeek day, bool isSelected) =>
        WriteAsync((client, id, sid, ct) => client.SetBreakTimeEntryDayAsync(id, sid, entryId, day, isSelected, ct));

    /// <inheritdoc />
    public Task<bool> SetDisplayTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WriteAsync((client, id, sid, ct) => client.SetDisplayTextAsync(id, sid, text, ct));
    }

    /// <inheritdoc />
    protected override Task<AccountRulesDto> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct) =>
        client.GetAccountRulesAsync(accountSid, ct);

    /// <inheritdoc />
    protected override string AccountSidOf(AccountRulesDto snapshot) => snapshot.AccountSid;

    /// <inheritdoc />
    protected override Guid? LastRequestOf(AccountRulesDto snapshot) => snapshot.LastChangeRequestId;
}
