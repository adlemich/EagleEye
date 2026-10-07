using System.Security.AccessControl;
using System.Security.Principal;

namespace EagleEye.Service.Diagnostics;

/// <summary>
/// The ACL of the service log folder <c>%ProgramData%\EagleEye\logs\</c> (US-003 AC-14, FR-SVC-100):
/// inheritance removed, full control for SYSTEM and Administrators only, inherited by sub-folders
/// and files, owner Administrators. Well-known SIDs, so localized Windows (e.g. "Administratoren")
/// works. Standard users get no access at all.
/// </summary>
public static class LogDirectorySecurity
{
    /// <summary>SID of the local SYSTEM account.</summary>
    public static readonly SecurityIdentifier System = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>SID of the local group Administrators.</summary>
    public static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>Creates the security descriptor for the log folder.</summary>
    public static DirectorySecurity Create()
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(Administrators);
        foreach (var sid in new[] { System, Administrators })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        return security;
    }
}
