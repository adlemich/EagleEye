using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Groups;

/// <summary>
/// Client side of the global state area "AppGroups" (US-008, FR-APP-042, FR-APP-043): the groups of the service PC and the
/// listed apps with their group. Every write returns <c>true</c> when a snapshot confirming it was applied, and
/// <c>false</c> on rejection, connection loss or timeout (AC-14).
/// </summary>
public interface IAppGroupsModel
{
    /// <summary>Raised (on any thread) when the load state or the snapshot changed.</summary>
    event Action? Changed;

    /// <summary>Whether the groups are available.</summary>
    AccountsLoadState LoadState { get; }

    /// <summary>The last applied snapshot, or null.</summary>
    AppGroupsDto? Snapshot { get; }

    /// <summary>Creates an empty group (AC-7).</summary>
    Task<bool> CreateAsync(string name);

    /// <summary>Renames a group (AC-9).</summary>
    Task<bool> RenameAsync(long groupId, string name);

    /// <summary>Deletes a group; its apps become ungrouped (AC-10).</summary>
    Task<bool> DeleteAsync(long groupId);

    /// <summary>Moves an app into a group, or out of every group with <paramref name="groupId"/> = null (AC-11 to AC-13).</summary>
    Task<bool> MoveAsync(string programPath, long? groupId);
}

/// <summary>
/// Client side of "AppGroups" (US-008 Step 8): fetched on every confirmed (re)connect, broadcasts applied by the revision
/// rule, writes confirmed with the 4 s timeout. The global area is the one fixed key of
/// <see cref="SelectedAccountModel{TSnapshot}"/>, as for the warning settings.
/// </summary>
public sealed class AppGroupsModel : SelectedAccountModel<AppGroupsDto>, IAppGroupsModel
{
    private const string AreaKey = "AppGroups";

    /// <summary>Creates the model and follows the gateway.</summary>
    public AppGroupsModel(IParentHubGateway gateway, TimeProvider timeProvider)
        : base(gateway, timeProvider, snapshot => snapshot.Revision)
    {
        gateway.AppGroupsChanged += OnBroadcast;
        SelectAccount(AreaKey);
    }

    /// <inheritdoc />
    public Task<bool> CreateAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return WriteAsync((client, id, _, ct) => client.CreateAppGroupAsync(id, name, ct));
    }

    /// <inheritdoc />
    public Task<bool> RenameAsync(long groupId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return WriteAsync((client, id, _, ct) => client.RenameAppGroupAsync(id, groupId, name, ct));
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long groupId) =>
        WriteAsync((client, id, _, ct) => client.DeleteAppGroupAsync(id, groupId, ct));

    /// <inheritdoc />
    public Task<bool> MoveAsync(string programPath, long? groupId)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return WriteAsync((client, id, _, ct) => client.MoveAppToGroupAsync(id, programPath, groupId, ct));
    }

    /// <inheritdoc />
    protected override Task<AppGroupsDto> FetchAsync(IParentHubClient client, string accountSid, CancellationToken ct) =>
        client.GetAppGroupsAsync(ct);

    /// <inheritdoc />
    protected override string AccountSidOf(AppGroupsDto snapshot) => AreaKey;

    /// <inheritdoc />
    protected override Guid? LastRequestOf(AppGroupsDto snapshot) => snapshot.LastChangeRequestId;
}
