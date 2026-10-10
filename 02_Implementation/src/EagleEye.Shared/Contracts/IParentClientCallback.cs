using EagleEye.Shared.Models;

namespace EagleEye.Shared.Contracts;

/// <summary>
/// Client-side callback interface for parent apps. The service invokes these methods on all
/// connected, paired parent apps (group <c>Parents</c>) to broadcast the state of a state area
/// (ADR-010): full snapshots with a revision, including the app whose write caused the change.
/// </summary>
public interface IParentClientCallback
{
    /// <summary>
    /// The account inventory or a selection changed (state area "UserAccounts", ADR-010). Sent to
    /// all paired apps, including the app whose write caused it. Apply only if
    /// <see cref="UserAccountListDto.Revision"/> is higher than the last applied revision.
    /// </summary>
    Task OnUserAccountsChanged(UserAccountListDto snapshot);

    /// <summary>
    /// The usage of one account on one day changed (ADR-012 §6). Sent to all paired apps at most once
    /// per account every 5 s while apps are active, and at local midnight with the new, empty today.
    /// Apply only if <see cref="DayUsageDto.Revision"/> is higher than the revision held for that day.
    /// </summary>
    Task OnDayUsageChanged(DayUsageDto snapshot);
}
