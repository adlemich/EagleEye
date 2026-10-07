using EagleEye.Service.UserAccounts;
using Xunit;

namespace EagleEye.Service.Tests.UserAccounts;

public sealed class AccountInventoryFilterTests
{
    private const string Machine = "S-1-5-21-1111111111-2222222222-3333333333";

    [Theory]
    [InlineData("500")]
    [InlineData("501")]
    [InlineData("503")]
    [InlineData("504")]
    public void IsBuiltIn_BuiltInRelativeId_ReturnsTrue(string rid)
    {
        Assert.True(AccountInventoryFilter.IsBuiltIn($"{Machine}-{rid}"));
    }

    [Theory]
    [InlineData(Machine + "-502")]
    [InlineData(Machine + "-1000")]
    [InlineData(Machine + "-1001")]
    [InlineData(Machine + "-5000")]
    [InlineData("S-1-5-18")]
    public void IsBuiltIn_OtherRelativeId_ReturnsFalse(string sid)
    {
        Assert.False(AccountInventoryFilter.IsBuiltIn(sid));
    }

    [Theory]
    [InlineData("")]
    [InlineData("500")]
    [InlineData("S-1-5-21-x")]
    [InlineData("S-1-5-21-")]
    [InlineData("S-1-5-21-+500")]
    [InlineData("S-1-5-21-99999999999")]
    public void IsBuiltIn_MalformedSid_ReturnsFalse(string sid)
    {
        Assert.False(AccountInventoryFilter.IsBuiltIn(sid));
    }

    [Theory]
    [InlineData("defaultuser0")]
    [InlineData("DefaultUser0")]
    [InlineData("DEFAULTUSER0")]
    public void IsSetupLeftover_DefaultUser0AnyCasing_ReturnsTrue(string name)
    {
        Assert.True(AccountInventoryFilter.IsSetupLeftover(name));
    }

    [Theory]
    [InlineData("defaultuser1")]
    [InlineData("defaultuser00")]
    [InlineData("xdefaultuser0")]
    [InlineData("defaultuser")]
    [InlineData("defaultuser100000")]
    [InlineData("max")]
    public void IsSetupLeftover_SimilarNames_ReturnsFalse(string name)
    {
        Assert.False(AccountInventoryFilter.IsSetupLeftover(name));
    }

    [Fact]
    public void Standard_KeepsOnlyStandardAccounts()
    {
        LocalAccountInfo[] accounts =
        [
            new($"{Machine}-500", "Administrator", null, true, true),
            new($"{Machine}-501", "Gast", null, true, false),
            new($"{Machine}-503", "DefaultAccount", null, true, false),
            new($"{Machine}-504", "WDAGUtilityAccount", null, true, false),
            new($"{Machine}-1000", "papa", "Michael Adler", false, true),
            new($"{Machine}-1001", "defaultuser0", null, false, false),
            new($"{Machine}-1002", "max", "Max Adler", false, false),
            new($"{Machine}-1003", "micha", "Michael Adler", false, false), // Microsoft-linked standard account
            new($"{Machine}-1004", "gaming", null, true, false),
            new($"{Machine}-501", "Renamed guest", null, false, false),
        ];

        var standard = AccountInventoryFilter.Standard(accounts);

        Assert.Equal(["max", "micha", "gaming"], standard.Select(a => a.UserName));
    }

    [Fact]
    public void Guards_Null_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AccountInventoryFilter.IsBuiltIn(null!));
        Assert.Throws<ArgumentNullException>(() => AccountInventoryFilter.IsSetupLeftover(null!));
        Assert.Throws<ArgumentNullException>(() => AccountInventoryFilter.Standard(null!));
    }
}
