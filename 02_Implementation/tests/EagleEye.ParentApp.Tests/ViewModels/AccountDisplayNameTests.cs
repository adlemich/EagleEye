using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AccountDisplayNameTests
{
    [Fact]
    public void Format_FullName_ShowsFullNameAndUserName()
    {
        Assert.Equal("Max Adler (max)", AccountDisplayName.Format(Account("Max Adler", false)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_NoFullName_ShowsUserNameOnly(string? fullName)
    {
        Assert.Equal("max", AccountDisplayName.Format(Account(fullName, false)));
    }

    [Theory]
    [InlineData("de-DE", "Max Adler (max) (deaktiviert)")]
    [InlineData("en-US", "Max Adler (max) (disabled)")]
    public void Format_Disabled_AppendsSuffix(string culture, string expected)
    {
        Assert.Equal(expected, TestSupport.InCulture(culture, () => AccountDisplayName.Format(Account("Max Adler", true))));
    }

    [Fact]
    public void Comparer_IgnoresCase()
    {
        Assert.Equal(0, AccountDisplayName.Comparer.Compare("max", "MAX"));
    }

    [Fact]
    public void Format_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AccountDisplayName.Format(null!));
    }

    private static UserAccountDto Account(string? fullName, bool disabled) => new("S-1-5-21-1-2-3-1001", "max", fullName, disabled, false);
}
