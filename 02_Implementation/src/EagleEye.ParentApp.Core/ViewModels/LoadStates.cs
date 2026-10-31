using EagleEye.ParentApp.Core.Accounts;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>Load state of a page part that needs two state areas (US-008: app rules and app groups).</summary>
internal static class LoadStates
{
    /// <summary>Ready when both are ready; otherwise the state of the first one that is not ready.</summary>
    public static AccountsLoadState Both(AccountsLoadState first, AccountsLoadState second) =>
        first == AccountsLoadState.Ready ? second : first;
}
