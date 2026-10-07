using EagleEye.Service.UserAccounts;
using Xunit;

namespace EagleEye.Service.Tests.UserAccounts;

public sealed class AccountInventoryTests
{
    private const string Machine = "S-1-5-21-1-2-3";
    private static readonly LocalAccountInfo Admin = new($"{Machine}-1000", "papa", null, false, true);
    private static readonly LocalAccountInfo Max = new($"{Machine}-1001", "max", "Max Adler", false, false);
    private static readonly LocalAccountInfo Anna = new($"{Machine}-1002", "anna", null, false, false);

    private static readonly AccountInventory Base = AccountInventory.Create([Admin, Max, Anna]);

    [Fact]
    public void Create_SplitsStandardAccountsAndAllSids()
    {
        Assert.Equal(
            (2, 3, true),
            (Base.StandardAccounts.Count, Base.AllSids.Count, Base.StandardAccounts.ContainsKey(Max.Sid.ToLowerInvariant())));
    }

    [Fact]
    public void HasSameContent_Equal_ReturnsTrue()
    {
        Assert.True(Base.HasSameContent(AccountInventory.Create([Anna, Max, Admin])));
    }

    public static TheoryData<string, LocalAccountInfo[]> Changes => new()
    {
        { "added", [Admin, Max, Anna, new($"{Machine}-1003", "lena", null, false, false)] },
        { "removed", [Admin, Max] },
        { "renamed", [Admin, Max with { UserName = "maximilian" }, Anna] },
        { "full name", [Admin, Max with { FullName = "Maximilian Adler" }, Anna] },
        { "disabled", [Admin, Max with { IsDisabled = true }, Anna] },
        { "became admin", [Admin, Max with { IsAdmin = true }, Anna] },
        { "admin became standard", [Admin with { IsAdmin = false }, Max, Anna] },
        { "admin added", [Admin, Max, Anna, new($"{Machine}-1009", "mama", null, false, true)] },
        { "admin deleted", [Max, Anna] },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void HasSameContent_Changed_ReturnsFalse(string change, LocalAccountInfo[] accounts)
    {
        Assert.False(Base.HasSameContent(AccountInventory.Create(accounts)), change);
    }

    [Fact]
    public void HasSameStandardAccounts_OnlyAdminChanged_ReturnsTrue()
    {
        Assert.True(Base.HasSameStandardAccounts(AccountInventory.Create([Max, Anna])));
    }

    [Fact]
    public void HasSameStandardAccounts_SameCountOtherSid_ReturnsFalse()
    {
        var other = AccountInventory.Create([Admin, Max, Anna with { Sid = $"{Machine}-1005" }]);

        Assert.False(Base.HasSameStandardAccounts(other));
    }

    [Fact]
    public void HasSameStandardAccounts_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Base.HasSameStandardAccounts(null!));
    }

    [Fact]
    public void Compare_NoPrevious_EverythingAdded()
    {
        var (added, removed, changed) = Base.Compare(null);

        Assert.Equal<IEnumerable<string>>(["anna", "max"], added);
        Assert.Equal((0, 0), (removed.Count, changed.Count));
    }

    [Fact]
    public void Compare_AddedRemovedChanged_ListsUserNamesSorted()
    {
        var next = AccountInventory.Create(
            [Admin, Max with { UserName = "maximilian" }, new($"{Machine}-1004", "Zoe", null, false, false), new($"{Machine}-1003", "lena", null, false, false)]);

        var (added, removed, changed) = next.Compare(Base);

        Assert.Equal<IEnumerable<string>>(["lena", "Zoe"], added);
        Assert.Equal<IEnumerable<string>>(["anna"], removed);
        Assert.Equal<IEnumerable<string>>(["maximilian"], changed);
    }

    [Fact]
    public void Create_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AccountInventory.Create(null!));
    }
}
