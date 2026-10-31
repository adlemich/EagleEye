using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Groups;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppGroupRulesSectionViewModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";

    private readonly Mock<IAppGroupRulesModel> _rules = new();
    private readonly Mock<IAppGroupsModel> _groups = new();
    private AccountsLoadState _rulesState = AccountsLoadState.Ready;
    private AccountsLoadState _groupsState = AccountsLoadState.Ready;
    private AppGroupRulesDto _rulesSnapshot = new(1, null, Kid, [new AppGroupRuleDto(7, 60, BreakTimeDays.Saturday | BreakTimeDays.Sunday)]);
    private AppGroupsDto _groupsSnapshot = new(
        1,
        null,
        [new AppGroupDto(7, "Games"), new AppGroupDto(8, "art")],
        [new GroupableAppDto("m", "Minecraft", 7), new GroupableAppDto("l", "launcher", 7), new GroupableAppDto("p", "Paint", null)]);

    public AppGroupRulesSectionViewModelTests()
    {
        _rules.SetupGet(r => r.LoadState).Returns(() => _rulesState);
        _rules.SetupGet(r => r.Snapshot).Returns(() => _rulesSnapshot);
        _groups.SetupGet(g => g.LoadState).Returns(() => _groupsState);
        _groups.SetupGet(g => g.Snapshot).Returns(() => _groupsSnapshot);
        _rules.Setup(r => r.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>())).ReturnsAsync(true);
        _rules.Setup(r => r.SetDayAsync(It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>())).ReturnsAsync(true);
    }

    [Theory]
    [InlineData("de-DE", AccountsLoadState.NotAvailable, AccountsLoadState.Ready, "Keine Daten verfügbar")]
    [InlineData("en-US", AccountsLoadState.Ready, AccountsLoadState.Loading, "Loading …")]
    [InlineData("en-US", AccountsLoadState.Ready, AccountsLoadState.Ready, "")]
    public void States_AndStatusText(string culture, AccountsLoadState rules, AccountsLoadState groups, string text)
    {
        (_rulesState, _groupsState) = (rules, groups);

        var (status, hasStatus, ready, rows) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.StatusText, vm.HasStatus, vm.IsReady, vm.Rows.Count + (vm.ShowNoGroups ? 10 : 0));
        });

        var isReady = text.Length == 0;
        Assert.Equal((text, !isReady, isReady, isReady ? 2 : 0), (status, hasStatus, ready, rows));
    }

    [Fact]
    public void Rows_SortedByName_MembersLine_DefaultRuleForAGroupWithoutRule()
    {
        var rows = TestSupport.InCulture("de-DE", () => Create().Rows.Select(r => (r.AppId, r.DisplayName, r.ProgramPath, r.HoursText, r.MinutesText, r.Saturday)).ToList());

        Assert.Equal([(8L, "art", "(keine Apps)", "0", "0", false), (7L, "Games", "launcher, Minecraft", "1", "0", true)], rows);
    }

    [Theory]
    [InlineData("de-DE", "Noch keine App-Gruppen. Du kannst sie auf der Seite App Gruppen anlegen.")]
    [InlineData("en-US", "No app groups yet. You can create them on the App Groups page.")]
    public void NoGroups_TextShown(string culture, string text)
    {
        _groupsSnapshot = new AppGroupsDto(1, null, [], []);

        var vm = Create();

        Assert.Equal((true, false, text), (vm.ShowNoGroups, vm.HasRows, TestSupport.InCulture(culture, () => AppTextsOf())));
    }

    [Fact]
    public void GroupsChanged_RowsFollow_InstancesKept()
    {
        var vm = Create();
        var games = vm.Rows[1];

        _groupsSnapshot = new AppGroupsDto(2, null, [new AppGroupDto(7, "Aaa")], [new GroupableAppDto("p", "Paint", 7)]);
        _groups.Raise(g => g.Changed += null);

        Assert.Equal((1, "Aaa", "Paint"), (vm.Rows.Count, games.DisplayName, games.ProgramPath));
        Assert.Same(games, vm.Rows[0]);
    }

    [Fact]
    public async Task Edits_SentToTheGroupRules()
    {
        var vm = Create();
        var games = vm.Rows[1];

        games.Monday = true;
        await games.LastWrite;
        games.MinutesText = "30";
        games.CommitMinutes();
        await games.LastWrite;

        _rules.Verify(r => r.SetDayAsync(7, DayOfWeek.Monday, true), Times.Once);
        _rules.Verify(r => r.SetBudgetAsync(7, AppBudgetPart.Minutes, 30), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task WriteFailed_MessageShown()
    {
        _rules.Setup(r => r.SetDayAsync(It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>())).ReturnsAsync(false);
        var vm = Create();

        TestSupport.InCulture("en-US", () => vm.Rows[1].Monday = true);
        await vm.Rows[1].LastWrite;

        Assert.Equal(("The change could not be saved. Please try again.", true), (vm.ErrorText, vm.HasError));
    }

    [Theory]
    [InlineData("24", true, "Enter hours between 0 and 23.")]
    [InlineData("60", false, "Enter minutes between 0 and 59.")]
    public void InvalidBudget_Message(string value, bool hours, string message)
    {
        var vm = Create();
        var row = vm.Rows[1];

        TestSupport.InCulture("en-US", () =>
        {
            if (hours)
            {
                row.HoursText = value;
            }
            else
            {
                row.MinutesText = value;
            }

            vm.FlushPendingEdits();
            return true;
        });

        Assert.Equal(message, vm.ErrorText);
        _rules.Verify(r => r.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void SelectAccount_Delegates()
    {
        Create().SelectAccount(Kid);

        _rules.Verify(r => r.SelectAccount(Kid), Times.Once);
    }

    [Fact]
    public void Guards_Throw()
    {
        var dispatcher = new ImmediateDispatcher();
        Assert.Throws<ArgumentNullException>(() => new AppGroupRulesSectionViewModel(null!, _groups.Object, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new AppGroupRulesSectionViewModel(_rules.Object, null!, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new AppGroupRulesSectionViewModel(_rules.Object, _groups.Object, null!));
    }

    private static string AppTextsOf() => EagleEye.ParentApp.Core.AppTexts.RulesNoGroups;

    private AppGroupRulesSectionViewModel Create() => new(_rules.Object, _groups.Object, new ImmediateDispatcher());
}
