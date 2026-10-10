using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class RulesViewModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Anna = "S-1-5-21-1-2-3-1004";
    private static readonly BreakTimeEntryDto First = new(1, true, 1200, 1439, BreakTimeDays.All);
    private static readonly BreakTimeEntryDto Second = new(2, false, 0, 540, BreakTimeDays.Saturday | BreakTimeDays.Sunday);

    private readonly Mock<IUserAccountsModel> _accounts = new();
    private readonly Mock<IAccountRulesModel> _rules = new();
    private AccountsLoadState _accountsState = AccountsLoadState.Ready;
    private UserAccountListDto _accountList = new(1, null,
    [
        new UserAccountDto(Kid, "max", "Max Adler", false, true),
        new UserAccountDto(Anna, "anna", null, false, true),
        new UserAccountDto("S-1-5-21-1-2-3-1005", "tom", null, false, false),
    ]);
    private AccountsLoadState _rulesState = AccountsLoadState.Ready;
    private AccountRulesDto? _snapshot;
    private string? _selected;

    public RulesViewModelTests()
    {
        _accounts.SetupGet(a => a.LoadState).Returns(() => _accountsState);
        _accounts.SetupGet(a => a.Snapshot).Returns(() => _accountList);
        _rules.SetupGet(r => r.LoadState).Returns(() => _rulesState);
        _rules.SetupGet(r => r.Snapshot).Returns(() => _snapshot);
        _rules.SetupGet(r => r.SelectedAccountSid).Returns(() => _selected);
        _rules.Setup(r => r.SelectAccount(It.IsAny<string?>())).Callback<string?>(sid => _selected = sid);
        _snapshot = Rules(First, Second);
    }

    // ---------- States and accounts ----------

    [Fact]
    public void Open_FirstControlledAccountSelectedAndFetched()
    {
        var vm = Create();

        Assert.Equal(["anna", "Max Adler (max)"], vm.Accounts.Select(a => a.DisplayName));
        Assert.Equal(Anna, vm.SelectedAccount!.Sid);
        _rules.Verify(r => r.SelectAccount(Anna), Times.Once);
    }

    [Theory]
    [InlineData("de-DE", AccountsLoadState.NotAvailable, AccountsLoadState.Ready, RulesState.NoData, "Keine Daten verfügbar")]
    [InlineData("en-US", AccountsLoadState.Loading, AccountsLoadState.Ready, RulesState.Loading, "Loading …")]
    [InlineData("de-DE", AccountsLoadState.Ready, AccountsLoadState.Loading, RulesState.Loading, "Wird geladen …")]
    [InlineData("en-US", AccountsLoadState.Ready, AccountsLoadState.NotAvailable, RulesState.NoData, "No data available")]
    [InlineData("de-DE", AccountsLoadState.Ready, AccountsLoadState.Ready, RulesState.Rules, "")]
    public void State_AndStatusText(string culture, AccountsLoadState accounts, AccountsLoadState rules, RulesState expected, string text)
    {
        _accountsState = accounts;
        _rulesState = rules;

        var (state, status, hasStatus, visible) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.State, vm.StatusText, vm.HasStatus, vm.IsRulesVisible);
        });

        Assert.Equal((expected, text, expected != RulesState.Rules, expected == RulesState.Rules), (state, status, hasStatus, visible));
    }

    [Theory]
    [InlineData("de-DE", "Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.")]
    [InlineData("en-US", "No accounts under parental control. Select accounts under Settings.")]
    public void NoControlledAccounts_Text(string culture, string expected)
    {
        _accountList = _accountList with { Accounts = [new UserAccountDto("S-1", "tom", null, false, false)] };

        var (state, text, selection) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.State, vm.StatusText, vm.IsAccountSelectionVisible);
        });

        Assert.Equal((RulesState.NoControlledAccounts, expected, false), (state, text, selection));
    }

    [Fact]
    public void SelectedAccountDisappears_FirstSelected()
    {
        var vm = Create();
        vm.SelectedAccount = vm.Accounts[1];

        _accountList = _accountList with { Accounts = [.. _accountList.Accounts.Where(a => a.Sid != Kid)] };
        _accounts.Raise(a => a.Changed += null);

        Assert.Equal(Anna, vm.SelectedAccount!.Sid);
    }

    [Fact]
    public void SelectedAccount_SameValue_NothingHappens()
    {
        var vm = Create();
        _rules.Invocations.Clear();

        vm.SelectedAccount = vm.Accounts[0];

        _rules.Verify(r => r.SelectAccount(It.IsAny<string?>()), Times.Never);
    }

    // ---------- Rows ----------

    [Fact]
    public void Rows_InCreationOrderWithValues()
    {
        var vm = Create();

        Assert.Equal([1L, 2L], vm.Rows.Select(r => r.EntryId));
        Assert.Equal(("20:00", "09:00", false), (vm.Rows[0].StartText, vm.Rows[1].EndText, vm.ShowNoEntries));
    }

    [Fact]
    public void Rows_MergedById_ExistingRowKept()
    {
        var vm = Create();
        var row = vm.Rows[0];

        _snapshot = Rules(First with { IsActive = false }, new BreakTimeEntryDto(7, false, 1200, 1439, BreakTimeDays.All));
        _rules.Raise(r => r.Changed += null);

        Assert.Same(row, vm.Rows[0]);
        Assert.Equal([1L, 7L], vm.Rows.Select(r => r.EntryId));
        Assert.False(row.IsActive);
    }

    [Fact]
    public void Rows_NoEntries_Text()
    {
        _snapshot = Rules();

        var vm = Create();

        Assert.Equal((true, true), (vm.ShowNoEntries, vm.CanAddEntry));
    }

    [Fact]
    public void Rows_ClearedWhenNotReady()
    {
        var vm = Create();

        _rulesState = AccountsLoadState.Loading;
        _rules.Raise(r => r.Changed += null);

        Assert.Equal((0, false, false), (vm.Rows.Count, vm.ShowNoEntries, vm.CanAddEntry));
    }

    // ---------- Add ----------

    [Fact]
    public async Task AddEntry_SentAndErrorCleared()
    {
        _rules.Setup(r => r.AddEntryAsync()).ReturnsAsync(true);
        var vm = Create();

        vm.AddEntryCommand.Execute(null);
        await vm.LastWrite;

        _rules.Verify(r => r.AddEntryAsync(), Times.Once);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void AddEntry_At20Entries_Disabled()
    {
        _snapshot = Rules([.. Enumerable.Range(1, BreakTimeRules.MaxEntriesPerAccount).Select(i => First with { EntryId = i })]);

        var vm = Create();

        Assert.Equal((false, false), (vm.CanAddEntry, vm.AddEntryCommand.CanExecute(null)));
    }

    [Theory]
    [InlineData("de-DE", "Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.")]
    [InlineData("en-US", "The change could not be saved. Please try again.")]
    public async Task Write_Fails_Ac18MessageThenClearedBySuccess(string culture, string expected)
    {
        var vm = Create();
        _rules.Setup(r => r.DeleteEntryAsync(1)).ReturnsAsync(false);
        await TestSupport.InCulture(culture, async () =>
        {
            vm.Rows[0].DeleteCommand.Execute(null);
            await Task.Delay(1);
            return true;
        });
        var error = vm.ErrorText;

        _rules.Setup(r => r.SetActiveAsync(2, true)).ReturnsAsync(true);
        vm.Rows[1].IsActive = true;
        await Task.Delay(1);

        Assert.Equal((expected, string.Empty), (error, vm.ErrorText));
    }

    [Theory]
    [InlineData("de-DE", "24:00", "Bitte eine Uhrzeit zwischen 00:00 und 23:59 eingeben.")]
    [InlineData("en-US", "19:00", "The end time must be later than the start time.")]
    public void RefusedTime_MessageShown(string culture, string typed, string expected)
    {
        var vm = Create();

        var error = TestSupport.InCulture(culture, () =>
        {
            vm.Rows[0].EndText = typed;
            vm.Rows[0].CommitEnd();
            return vm.ErrorText;
        });

        Assert.Equal((expected, true), (error, vm.HasError));
    }

    [Theory]
    [InlineData("de-DE", "Mindestens ein Tag muss ausgewählt sein.")]
    [InlineData("en-US", "Select at least one day.")]
    public void RefusedDay_MessageShown(string culture, string expected)
    {
        _snapshot = Rules(First with { Days = BreakTimeDays.Monday });
        var vm = Create();

        var error = TestSupport.InCulture(culture, () =>
        {
            vm.Rows[0].Monday = false;
            return vm.ErrorText;
        });

        Assert.Equal(expected, error);
    }

    // ---------- Display text ----------

    [Fact]
    public void DisplayText_ShowsTheConfirmedText()
    {
        _snapshot = Rules() with { DisplayText = "Hallo\n\U0001F60A" };

        Assert.Equal("Hallo\n\U0001F60A", Create().DisplayText);
    }

    [Fact]
    public async Task DisplayText_ChangedAndLeft_SentNormalized()
    {
        var vm = Create();

        vm.DisplayText = "Neu\r\U0001F44D\r\nEnde";
        vm.CommitDisplayText();
        await vm.LastWrite;

        _rules.Verify(r => r.SetDisplayTextAsync("Neu\n\U0001F44D\nEnde"), Times.Once);
    }

    [Fact]
    public void DisplayText_OnlyLineBreakFormChanged_NotSent()
    {
        _snapshot = Rules() with { DisplayText = "a\nb" };
        var vm = Create();

        vm.DisplayText = "a\rb";
        vm.CommitDisplayText();

        _rules.Verify(r => r.SetDisplayTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DisplayText_TypedBackToTheConfirmed_NotSent()
    {
        var vm = Create();
        vm.DisplayText = "x";
        vm.DisplayText = "Text";

        vm.CommitDisplayText();

        _rules.Verify(r => r.SetDisplayTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DisplayText_Emptied_SentSoTheServiceRestoresTheDefault()
    {
        var vm = Create();

        vm.DisplayText = string.Empty;
        vm.CommitDisplayText();
        await vm.LastWrite;

        _rules.Verify(r => r.SetDisplayTextAsync(string.Empty), Times.Once);
    }

    [Fact]
    public void DisplayText_BeingTyped_NotOverwrittenBySnapshots()
    {
        var vm = Create();
        vm.DisplayText = "getippt";

        _snapshot = Rules() with { DisplayText = "von B", Revision = 9 };
        _rules.Raise(r => r.Changed += null);

        Assert.Equal("getippt", vm.DisplayText);
    }

    [Fact]
    public void DisplayText_NullBecomesEmpty_AndMaxLength()
    {
        var vm = Create();

        vm.DisplayText = null!;

        Assert.Equal((string.Empty, 500), (vm.DisplayText, vm.DisplayTextMaxLength));
    }

    [Fact]
    public void DisplayText_EmptyWhenNotReady()
    {
        _rulesState = AccountsLoadState.NotAvailable;

        Assert.Equal(string.Empty, Create().DisplayText);
    }

    // ---------- Flush ----------

    [Fact]
    public async Task AccountSwitch_FlushesTypedTimesAndTextForTheOldAccount()
    {
        var vm = Create();
        var sentFor = new List<string?>();
        _rules.Setup(r => r.SetDisplayTextAsync(It.IsAny<string>())).Callback(() => sentFor.Add(_selected)).ReturnsAsync(true);
        _rules.Setup(r => r.SetTimeAsync(1, BreakTimeBoundary.Start, 1080)).Callback(() => sentFor.Add(_selected)).ReturnsAsync(true);
        vm.Rows[0].StartText = "18:00";
        vm.DisplayText = "neu";

        vm.SelectedAccount = vm.Accounts[1];
        await Task.Delay(1);

        Assert.Equal([Anna, Anna], sentFor);
        Assert.Equal(Kid, _selected);
    }

    [Fact]
    public void FlushPendingEdits_NothingTyped_NothingSent()
    {
        var vm = Create();
        _rules.Invocations.Clear();

        vm.FlushPendingEdits();

        Assert.Empty(_rules.Invocations.Where(i => i.Method.Name.StartsWith("Set", StringComparison.Ordinal)));
    }

    [Fact]
    public void AllAccountsUnticked_SelectionClearedAndRulesDeselected()
    {
        var vm = Create();

        _accountList = _accountList with { Accounts = [] };
        _accounts.Raise(a => a.Changed += null);

        Assert.Null(vm.SelectedAccount);
        _rules.Verify(r => r.SelectAccount(null), Times.Once);
    }

    [Theory]
    [InlineData(AccountsLoadState.Ready, true)]
    [InlineData(AccountsLoadState.Loading, true)]
    [InlineData(AccountsLoadState.NotAvailable, false)]
    public void AccountSelection_VisibleWithRulesAndWhileLoading(AccountsLoadState rules, bool expected)
    {
        _rulesState = rules;

        Assert.Equal(expected, Create().IsAccountSelectionVisible);
    }

    [Fact]
    public void DisplayText_SameValueSetAgain_NotDirty()
    {
        var vm = Create();

        vm.DisplayText = "Text";
        vm.CommitDisplayText();

        _rules.Verify(r => r.SetDisplayTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DisplayText_TypedWhileNotAvailable_ComparedWithEmpty()
    {
        _rulesState = AccountsLoadState.NotAvailable;
        _snapshot = null;
        var vm = Create();

        vm.DisplayText = "x";
        vm.CommitDisplayText();

        _rules.Verify(r => r.SetDisplayTextAsync("x"), Times.Once);
    }

    [Fact]
    public void DisplayText_OtherAppSavedTheSameText_NotSentAgain()
    {
        var vm = Create();
        vm.DisplayText = "neu";
        _snapshot = Rules() with { DisplayText = "neu", Revision = 9 };
        _rules.Raise(r => r.Changed += null);

        vm.CommitDisplayText();

        _rules.Verify(r => r.SetDisplayTextAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Guards_Throw()
    {
        var dispatcher = new ImmediateDispatcher();
        Assert.Throws<ArgumentNullException>(() => new RulesViewModel(null!, _rules.Object, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new RulesViewModel(_accounts.Object, null!, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new RulesViewModel(_accounts.Object, _rules.Object, null!));
    }

    private RulesViewModel Create() => new(_accounts.Object, _rules.Object, new ImmediateDispatcher());

    private AccountRulesDto Rules(params BreakTimeEntryDto[] entries) => new(5, null, _selected ?? Anna, entries, "Text", false);
}
