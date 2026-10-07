using System.Security.AccessControl;
using System.Security.Principal;
using EagleEye.Service.Diagnostics;
using Xunit;

namespace EagleEye.Service.Tests.Diagnostics;

public sealed class LogDirectorySecurityTests
{
    private static readonly DirectorySecurity Security = LogDirectorySecurity.Create();

    private static List<FileSystemAccessRule> Rules =>
        Security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToList();

    [Fact]
    public void Create_InheritanceIsRemoved()
    {
        Assert.True(Security.AreAccessRulesProtected);
    }

    [Fact]
    public void Create_ExactlySystemAndAdministrators()
    {
        Assert.Equal(
            ["S-1-5-18", "S-1-5-32-544"],
            Rules.Select(r => r.IdentityReference.Value).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Create_RulesAreFullControlAllowInheritedByFoldersAndFiles()
    {
        Assert.All(Rules, rule => Assert.Equal(
            (FileSystemRights.FullControl, AccessControlType.Allow, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None),
            (rule.FileSystemRights, rule.AccessControlType, rule.InheritanceFlags, rule.PropagationFlags)));
    }

    [Theory]
    [InlineData("S-1-5-32-545")] // Users
    [InlineData("S-1-5-11")] // Authenticated Users
    [InlineData("S-1-1-0")] // Everyone
    public void Create_NoRuleForStandardUsers(string sid)
    {
        Assert.DoesNotContain(Rules, r => r.IdentityReference.Value == sid);
    }

    [Fact]
    public void Create_OwnerIsAdministrators()
    {
        Assert.Equal("S-1-5-32-544", Security.GetOwner(typeof(SecurityIdentifier))?.Value);
    }
}
