namespace EagleEye.ParentApp.Core.Accounts;

/// <summary>Whether the account list of the service PC is available in the app (US-003 AC-7, AC-8, AC-13).</summary>
public enum AccountsLoadState
{
    /// <summary>Not connected, or the fetch failed: no data is shown (OQ-5).</summary>
    NotAvailable,

    /// <summary>Connected; the list is being fetched.</summary>
    Loading,

    /// <summary>A snapshot of the service has been applied.</summary>
    Ready,
}
