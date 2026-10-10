using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class ControlledAccountSelectionTests
{
    private readonly Mock<IUserAccountsModel> _accounts = new();
    private readonly ControlledAccountSelection _selection = new();

    [Fact]
    public void Merge_ControlledAccountsSortedByShownName()
    {
        Ready(Account("2", "zoe", true), Account("1", "anna", true), Account("3", "tom", false));

        TestSupport.InCulture("de-DE", () => { _selection.Merge(_accounts.Object); return 0; });

        Assert.Equal(["anna", "zoe"], _selection.Accounts.Select(a => a.DisplayName));
    }

    [Fact]
    public void Merge_NotReady_Empty()
    {
        Ready(Account("1", "anna", true));
        _selection.Merge(_accounts.Object);
        _accounts.SetupGet(a => a.LoadState).Returns(AccountsLoadState.Loading);

        _selection.Merge(_accounts.Object);

        Assert.Empty(_selection.Accounts);
    }

    [Fact]
    public void Choose_KeepsTheCurrentOrTakesTheFirst()
    {
        Ready(Account("1", "anna", true), Account("2", "zoe", true));
        _selection.Merge(_accounts.Object);

        Assert.Equal("2", _selection.Choose(new AccountOption("2", "old name"))!.Sid);
        Assert.Equal("1", _selection.Choose(new AccountOption("9", "gone"))!.Sid);
        Assert.Equal("1", _selection.Choose(null)!.Sid);
    }

    [Fact]
    public void Choose_NoAccounts_Null()
    {
        Assert.Null(_selection.Choose(null));
    }

    [Fact]
    public void Merge_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _selection.Merge(null!));
    }

    private void Ready(params UserAccountDto[] accounts)
    {
        _accounts.SetupGet(a => a.LoadState).Returns(AccountsLoadState.Ready);
        _accounts.SetupGet(a => a.Snapshot).Returns(new UserAccountListDto(1, null, accounts));
    }

    private static UserAccountDto Account(string sid, string name, bool controlled) => new(sid, name, null, false, controlled);
}
