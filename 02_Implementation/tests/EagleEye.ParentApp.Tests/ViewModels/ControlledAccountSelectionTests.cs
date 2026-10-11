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
    private readonly List<AccountOption?> _selected = [];

    [Fact]
    public void Update_ControlledAccountsSortedByShownName_FirstSelected()
    {
        Ready(Account("2", "zoe", true), Account("1", "anna", true), Account("3", "tom", false));

        TestSupport.InCulture("de-DE", () => { Update(null); return 0; });

        Assert.Equal(["anna", "zoe"], _selection.Accounts.Select(a => a.DisplayName));
        Assert.Equal("1", _selected.Single()!.Sid);
    }

    [Fact]
    public void Update_NotReady_EmptyAndNothingSelected()
    {
        Ready(Account("1", "anna", true));
        Update(null);
        _accounts.SetupGet(a => a.LoadState).Returns(AccountsLoadState.Loading);
        _selected.Clear();

        Update(_selection.Accounts[0]);

        Assert.Empty(_selection.Accounts);
        Assert.Equal([null, null], _selected);
    }

    [Fact]
    public void Update_SelectedStillListed_KeptWithoutClearing()
    {
        Ready(Account("1", "anna", true), Account("2", "zoe", true));
        Update(null);
        _selected.Clear();

        Update(_selection.Accounts[1]);

        Assert.Equal("2", _selected.Single()!.Sid);
    }

    [Fact]
    public void Update_SelectedRemoved_ClearedBeforeTheEntryIsRemoved()
    {
        Ready(Account("1", "anna", true), Account("2", "zoe", true));
        Update(null);
        var zoe = _selection.Accounts[1];
        var countWhenCleared = -1;
        Ready(Account("1", "anna", true));

        _selection.Update(_accounts.Object, zoe, account =>
        {
            if (account is null)
            {
                countWhenCleared = _selection.Accounts.Count;
            }

            _selected.Add(account);
        });

        Assert.Equal((2, "1"), (countWhenCleared, _selected[^1]!.Sid));
    }

    [Fact]
    public void Update_RenamedSelectedAccount_SelectedAgainBySid()
    {
        Ready(Account("1", "anna", true));
        Update(null);
        var old = _selection.Accounts[0];
        Ready(Account("1", "anna2", true));
        _selected.Clear();

        Update(old);

        Assert.Equal([null, new AccountOption("1", "anna2")], _selected);
    }

    [Fact]
    public void Update_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => _selection.Update(null!, null, _ => { }));
        Assert.Throws<ArgumentNullException>(() => _selection.Update(_accounts.Object, null, null!));
    }

    private void Update(AccountOption? current) => _selection.Update(_accounts.Object, current, _selected.Add);

    private void Ready(params UserAccountDto[] accounts)
    {
        _accounts.SetupGet(a => a.LoadState).Returns(AccountsLoadState.Ready);
        _accounts.SetupGet(a => a.Snapshot).Returns(new UserAccountListDto(1, null, accounts));
    }

    private static UserAccountDto Account(string sid, string name, bool controlled) => new(sid, name, null, false, controlled);
}
