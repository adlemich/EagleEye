namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>What the section "User accounts on the EagleEye PC" shows (US-003 AC-7 to AC-13).</summary>
public enum AccountsSectionState
{
    /// <summary>Not paired, not connected, or the fetch failed: "No data available".</summary>
    NoData,

    /// <summary>Fetching after a (re)connect: "Loading …".</summary>
    Loading,

    /// <summary>Connected, no standard accounts: "No non-admin accounts available".</summary>
    NoAccounts,

    /// <summary>Connected, the list of accounts.</summary>
    List,
}
