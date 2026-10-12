using System.ComponentModel;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppRulesSectionViewModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private static readonly AppRuleDto Editor = new(1, "Editor", @"C:\Windows\notepad.exe", 45, BreakTimeDays.Monday);
    private static readonly AppRuleDto Paint = new(2, "paint", @"C:\Apps\paint.exe", 0, BreakTimeDays.None);
    private static readonly AppRuleDto Zoo = new(3, "Zoo", @"C:\Apps\zoo.exe", 90, BreakTimeDays.All);

    private readonly Mock<IAppRulesModel> _model = new();
    private AccountsLoadState _state = AccountsLoadState.Ready;
    private AppRulesDto? _snapshot;

    public AppRulesSectionViewModelTests()
    {
        _model.SetupGet(m => m.LoadState).Returns(() => _state);
        _model.SetupGet(m => m.Snapshot).Returns(() => _snapshot);
        _model.Setup(m => m.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>())).ReturnsAsync(true);
        _model.Setup(m => m.SetDayAsync(It.IsAny<long>(), It.IsAny<DayOfWeek>(), It.IsAny<bool>())).ReturnsAsync(true);
        _model.Setup(m => m.SetBudgetTextAsync(It.IsAny<string>())).ReturnsAsync(true);
        _snapshot = Snapshot(1, Zoo, Editor, Paint);
    }

    // ---------- States, sorting, merging ----------

    [Theory]
    [InlineData("de-DE", AccountsLoadState.NotAvailable, "Keine Daten verfügbar")]
    [InlineData("en-US", AccountsLoadState.Loading, "Loading …")]
    [InlineData("en-US", AccountsLoadState.Ready, "")]
    public void States_AndStatusText(string culture, AccountsLoadState state, string text)
    {
        _state = state;

        var (status, hasStatus, ready) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.StatusText, vm.HasStatus, vm.IsReady);
        });

        Assert.Equal((text, state != AccountsLoadState.Ready, state == AccountsLoadState.Ready), (status, hasStatus, ready));
    }

    [Fact]
    public void Rows_SortedByNameIgnoringCaseThenById()
    {
        var other = new AppRuleDto(4, "editor", "x", 0, BreakTimeDays.None);
        _snapshot = Snapshot(1, Zoo, other, Editor, Paint);

        var vm = Create();

        Assert.Equal([1L, 4L, 2L, 3L], vm.Rows.Select(r => r.AppId));
    }

    [Fact]
    public void NewRecord_InsertedInPlace_ExistingRowsKept()
    {
        var vm = Create();
        var editorRow = vm.Rows[0];
        var game = new AppRuleDto(5, "Game", "g", 0, BreakTimeDays.None);

        _snapshot = Snapshot(2, Zoo, Editor, Paint, game);
        _model.Raise(m => m.Changed += null);

        Assert.Equal([1L, 5L, 2L, 3L], vm.Rows.Select(r => r.AppId));
        Assert.Same(editorRow, vm.Rows[0]);
    }

    [Fact]
    public void RenamedRecord_MovedToItsPlace_RemovedRecordGone()
    {
        var vm = Create();
        var zooRow = vm.Rows[2];

        _snapshot = Snapshot(2, Zoo with { DisplayName = "Aquarium" }, Editor);
        _model.Raise(m => m.Changed += null);

        Assert.Equal([3L, 1L], vm.Rows.Select(r => r.AppId));
        Assert.Same(zooRow, vm.Rows[0]);
        Assert.Equal("Aquarium", zooRow.DisplayName);
    }

    [Fact]
    public void NoApps_TextShown_NotReady_NoRows()
    {
        _snapshot = Snapshot(1);
        Assert.True(Create().ShowNoApps);

        _state = AccountsLoadState.Loading;
        var vm = Create();
        Assert.Equal((false, 0), (vm.ShowNoApps, vm.Rows.Count));
    }

    [Fact]
    public void SelectAccount_Delegates()
    {
        Create().SelectAccount(Kid);

        _model.Verify(m => m.SelectAccount(Kid), Times.Once);
    }

    // ---------- Budget text (AC-10) ----------

    [Theory]
    [InlineData("de-DE", "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.")]
    [InlineData("en-US", "You have used up your time! Sorry, that's it for now.")]
    public void BudgetText_Default_InTheAppsLanguage(string culture, string expected)
    {
        Assert.Equal(expected, TestSupport.InCulture(culture, () => Create().BudgetText));
    }

    [Fact]
    public async Task BudgetText_ChangedAndLeft_Sent()
    {
        var vm = Create();
        vm.BudgetText = "Schluss\r\nfür heute \U0001F60A";

        vm.CommitBudgetText();
        await vm.LastWrite;

        _model.Verify(m => m.SetBudgetTextAsync("Schluss\nfür heute \U0001F60A"), Times.Once);
    }

    [Fact]
    public void BudgetText_EmptyWhileDefault_NothingSentDefaultShownAgain()
    {
        var vm = TestSupport.InCulture("de-DE", Create);

        TestSupport.InCulture("de-DE", () =>
        {
            vm.BudgetText = string.Empty;
            vm.CommitBudgetText();
            return true;
        });

        _model.Verify(m => m.SetBudgetTextAsync(It.IsAny<string>()), Times.Never);
        Assert.Equal("Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.", vm.BudgetText);
    }

    [Fact]
    public async Task BudgetText_CustomEmptied_SentEmpty()
    {
        _snapshot = _snapshot! with { BudgetText = "Ende" };
        var vm = Create();
        vm.BudgetText = string.Empty;

        vm.CommitBudgetText();
        await vm.LastWrite;

        _model.Verify(m => m.SetBudgetTextAsync(string.Empty), Times.Once);
    }

    [Fact]
    public void BudgetText_NotDirty_NothingSent_TypingKeptAgainstSnapshots()
    {
        _snapshot = _snapshot! with { BudgetText = "Ende" };
        var vm = Create();
        vm.CommitBudgetText();
        vm.BudgetText = "Ende";
        vm.BudgetText = "getippt";

        _snapshot = _snapshot with { BudgetText = "von B", Revision = 9 };
        _model.Raise(m => m.Changed += null);

        Assert.Equal("getippt", vm.BudgetText);
        _model.Verify(m => m.SetBudgetTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BudgetText_NullBecomesEmpty_MaxLength_EmptyWhenNotReady()
    {
        var vm = Create();
        vm.BudgetText = null!;
        Assert.Equal((string.Empty, 500), (vm.BudgetText, vm.BudgetTextMaxLength));

        _state = AccountsLoadState.NotAvailable;
        _snapshot = null;
        Assert.Equal(string.Empty, Create().BudgetText);
    }

    [Fact]
    public async Task BudgetText_TypedWhileNoSnapshot_ComparedWithEmpty()
    {
        _snapshot = null;
        _state = AccountsLoadState.NotAvailable;
        var vm = Create();
        vm.BudgetText = "x";

        vm.CommitBudgetText();
        await vm.LastWrite;

        _model.Verify(m => m.SetBudgetTextAsync("x"), Times.Once);
    }

    // ---------- Rows (AC-7, AC-8, AC-12) ----------

    [Fact]
    public void Row_ShowsHoursMinutesDaysAndTooltip()
    {
        var row = Create().Rows[0];

        Assert.Equal(("0", "45", true, false, @"C:\Windows\notepad.exe"), (row.HoursText, row.MinutesText, row.Monday, row.Tuesday, row.ProgramPath));
    }

    [Fact]
    public async Task Row_ValidHours_SentOnCommitOnly()
    {
        _model.Setup(m => m.SetBudgetAsync(1, AppBudgetPart.Hours, 2))
            .Callback(() => _snapshot = Snapshot(2, Zoo, Editor with { BudgetMinutes = 165 }, Paint)).ReturnsAsync(true);
        var row = Create().Rows[0];
        row.HoursText = "02";
        _model.Verify(m => m.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>()), Times.Never);

        row.CommitHours();
        await row.LastWrite;

        _model.Verify(m => m.SetBudgetAsync(1, AppBudgetPart.Hours, 2), Times.Once);
        Assert.Equal("2", row.HoursText);
    }

    [Fact]
    public async Task Row_ValidMinutes_Sent()
    {
        var row = Create().Rows[0];
        row.MinutesText = "5";

        row.CommitMinutes();
        await row.LastWrite;

        _model.Verify(m => m.SetBudgetAsync(1, AppBudgetPart.Minutes, 5), Times.Once);
    }

    [Theory]
    [InlineData("24", true)]
    [InlineData("abc", true)]
    [InlineData("", true)]
    [InlineData("60", false)]
    [InlineData("1.5", false)]
    public void Row_Invalid_RevertedWithMessage(string text, bool hours)
    {
        var vm = TestSupport.InCulture("en-US", Create);
        var row = vm.Rows[0];

        TestSupport.InCulture("en-US", () =>
        {
            if (hours)
            {
                row.HoursText = text;
                row.CommitHours();
            }
            else
            {
                row.MinutesText = text;
                row.CommitMinutes();
            }

            return true;
        });

        Assert.Equal(hours ? "Enter hours between 0 and 23." : "Enter minutes between 0 and 59.", vm.ErrorText);
        Assert.True(vm.HasError);
        Assert.Equal(("0", "45"), (row.HoursText, row.MinutesText));
        _model.Verify(m => m.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Row_Unchanged_NotSent_NotDirtyNothing()
    {
        var row = Create().Rows[0];
        row.CommitHours();
        row.MinutesText = "45";
        row.MinutesText = "045";
        row.MinutesText = "45";

        row.CommitMinutes();

        _model.Verify(m => m.SetBudgetAsync(It.IsAny<long>(), It.IsAny<AppBudgetPart>(), It.IsAny<int>()), Times.Never);
        Assert.False(row.HasUncommittedText);
    }

    [Fact]
    public async Task Row_DaysSentAtTheClick_AllUntickedAllowed_SameValueIgnored()
    {
        var row = Create().Rows[0];

        row.Monday = true;
        row.Monday = false;
        row.Tuesday = true;
        row.Wednesday = true;
        row.Thursday = true;
        row.Friday = true;
        row.Saturday = true;
        row.Sunday = true;
        await row.LastWrite;

        _model.Verify(m => m.SetDayAsync(1, DayOfWeek.Monday, false), Times.Once);
        _model.Verify(m => m.SetDayAsync(1, It.IsAny<DayOfWeek>(), true), Times.Exactly(6));
    }

    [Fact]
    public void Row_PendingAndTypedFields_NotOverwrittenBySnapshots()
    {
        var pending = new TaskCompletionSource<bool>();
        _model.Setup(m => m.SetDayAsync(1, DayOfWeek.Tuesday, true)).Returns(pending.Task);
        var vm = Create();
        var row = vm.Rows[0];
        row.Tuesday = true;
        row.MinutesText = "1";

        _snapshot = Snapshot(2, Zoo, Editor with { BudgetMinutes = 125 }, Paint);
        _model.Raise(m => m.Changed += null);

        Assert.Equal((true, "2", "1", true), (row.Tuesday, row.HoursText, row.MinutesText, row.HasUncommittedText));
        pending.SetResult(true);
    }

    [Fact]
    public async Task Row_WriteFails_MessageAndConfirmedValueShownAgain_NextSuccessClearsIt()
    {
        _model.Setup(m => m.SetDayAsync(1, DayOfWeek.Sunday, true)).ReturnsAsync(false);
        var vm = TestSupport.InCulture("en-US", Create);
        var row = vm.Rows[0];

        TestSupport.InCulture("en-US", () => row.Sunday = true);
        await row.LastWrite;
        Assert.Equal((false, "The change could not be saved. Please try again."), (row.Sunday, TestSupport.InCulture("en-US", () => vm.ErrorText)));

        row.Saturday = true;
        await row.LastWrite;
        Assert.Equal(string.Empty, vm.ErrorText);
    }

    [Fact]
    public void FlushPendingEdits_CommitsRowsAndText()
    {
        var vm = Create();
        vm.Rows[0].HoursText = "3";
        vm.BudgetText = "neu";

        vm.FlushPendingEdits();

        _model.Verify(m => m.SetBudgetAsync(1, AppBudgetPart.Hours, 3), Times.Once);
        _model.Verify(m => m.SetBudgetTextAsync("neu"), Times.Once);
    }

    [Fact]
    public void PropertyChanged_RaisedForStateAndError()
    {
        _state = AccountsLoadState.Loading;
        var vm = Create();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _state = AccountsLoadState.Ready;
        _model.Raise(m => m.Changed += null);

        Assert.Contains(nameof(AppRulesSectionViewModel.IsReady), changed);
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new AppRulesSectionViewModel(null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new AppRulesSectionViewModel(_model.Object, null!));
    }

    private AppRulesSectionViewModel Create() => new(_model.Object, new ImmediateDispatcher());

    private static AppRulesDto Snapshot(long revision, params AppRuleDto[] apps) => new(revision, null, Kid, apps, null);
}
