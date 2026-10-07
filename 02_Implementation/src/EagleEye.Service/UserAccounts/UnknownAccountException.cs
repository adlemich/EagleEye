namespace EagleEye.Service.UserAccounts;

/// <summary>The SID is not a standard account of the current inventory (e.g. it just became an admin, AC-20).</summary>
public sealed class UnknownAccountException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public UnknownAccountException()
        : base("The account is not in the inventory of standard accounts.")
    {
    }
}
