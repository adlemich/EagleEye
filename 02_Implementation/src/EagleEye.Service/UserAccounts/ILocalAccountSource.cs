namespace EagleEye.Service.UserAccounts;

/// <summary>Reads the local accounts of the PC from Windows.</summary>
public interface ILocalAccountSource
{
    /// <summary>Returns all local user accounts (standard, admin and built-in).</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">Windows reported an error.</exception>
    IReadOnlyList<LocalAccountInfo> GetAccounts();
}
