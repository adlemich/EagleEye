namespace EagleEye.Service.UserAccounts;

/// <summary>The local accounts could not be read yet; the inventory is not available.</summary>
public sealed class AccountInventoryUnavailableException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public AccountInventoryUnavailableException()
        : base("The account inventory is not available.")
    {
    }
}
