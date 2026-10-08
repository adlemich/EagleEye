using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Reports;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class ReportsViewModelTests
{
    private const string Max = "S-1-5-21-1-2-3-1001";
    private const string Anna = "S-1-5-21-1-2-3-1002";
    private const string Lena = "S-1-5-21-1-2-3-1003";
    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly FakeAccountsModel _accounts = new();
    private readonly FakeUsageModel _usage = new();
    private readonly ReportsViewModel _viewModel;

    public ReportsViewModelTests()
    {
        _viewModel = new ReportsViewModel(_accounts, _usage, new ImmediateDispatcher());
    }

    [Theory]
    [InlineData("de-DE", "Keine Daten verfügbar")]
    [InlineData("en-US", "No data available")]
    public void NotConnected_NoData(string culture, string text)
    {
        Assert.Equal((ReportsState.NoData, text, true, false, false),
            (_viewModel.State, TestSupport.InCulture(culture, () => _viewModel.StatusText), _viewModel.HasStatus, _viewModel.IsReportVisible, _viewModel.IsAccountSelectionVisible));
    }

    [Fact]
    public void AccountsLoading_Loading()
    {
        _accounts.Publish(AccountsLoadState.Loading, null);

        Assert.Equal((ReportsState.Loading, "Wird geladen …"), (_viewModel.State, TestSupport.InCulture("de-DE", () => _viewModel.StatusText)));
    }

    [Theory]
    [InlineData("de-DE", "Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.")]
    [InlineData("en-US", "No accounts under parental control. Select accounts under Settings.")]
    public void NoControlledAccounts_Text(string culture, string text)
    {
        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", false)));

        Assert.Equal((ReportsState.NoControlledAccounts, text, null), (_viewModel.State, TestSupport.InCulture(culture, () => _viewModel.StatusText), _usage.Selected));
    }

    [Fact]
    public void ControlledAccounts_OnlyTickedInUs003OrderFirstSelected()
    {
        TestSupport.InCulture("de-DE", () =>
        {
            _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", true, "Max Adler"), Account(Anna, "anna", true), Account(Lena, "lena", false)));
            return 0;
        });

        Assert.Equal(["anna", "Max Adler (max)"], _viewModel.Accounts.Select(a => a.DisplayName));
        Assert.Equal((Anna, Anna), (_viewModel.SelectedAccount!.Sid, _usage.Selected));
        Assert.Equal("anna", _viewModel.SelectedAccount.ToString());
    }

    [Fact]
    public void UsageLoading_LoadingWithAccountSelection()
    {
        ConnectWithAccounts();
        _usage.Publish(AccountsLoadState.Loading, null);

        Assert.Equal((ReportsState.Loading, true), (_viewModel.State, _viewModel.IsAccountSelectionVisible));
    }

    [Fact]
    public void UsageNotAvailable_NoData()
    {
        ConnectWithAccounts();

        Assert.Equal(ReportsState.NoData, _viewModel.State);
    }

    [Fact]
    public void Report_TodayFirstThenOlderDays()
    {
        ConnectWithAccounts();

        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today.AddDays(-3), 10), Day(Today, 5), Day(Today.AddDays(-1), 7));

        Assert.Equal((ReportsState.Report, false, true, string.Empty), (_viewModel.State, _viewModel.HasStatus, _viewModel.IsAccountSelectionVisible, _viewModel.StatusText));
        Assert.Equal([Today, Today.AddDays(-1), Today.AddDays(-3)], _viewModel.Days.Select(d => d.Day));
        Assert.True(_viewModel.Days[0].IsToday);
    }

    [Fact]
    public void Report_NoUsageToday_TodayShownEmpty()
    {
        ConnectWithAccounts();

        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today.AddDays(-1), 7));

        Assert.Equal((Today, true), (_viewModel.Days[0].Day, _viewModel.Days[0].ShowNoUsageToday));
    }

    [Fact]
    public void Report_MergeKeepsDayInstancesAndRemovesOld()
    {
        ConnectWithAccounts();
        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today, 5), Day(Today.AddDays(-1), 7));
        var today = _viewModel.Days[0];

        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today, 65), Day(Today.AddDays(-2), 7));

        Assert.Same(today, _viewModel.Days[0]);
        Assert.Equal([Today, Today.AddDays(-2)], _viewModel.Days.Select(d => d.Day));
        Assert.Equal("00:01", today.Rows.Single().UsageText);
    }

    [Fact]
    public void Midnight_NewTodayMovesFirstAndOldTodayBecomesPast()
    {
        ConnectWithAccounts();
        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today, 5));

        _usage.Publish(AccountsLoadState.Ready, Today.AddDays(1), Day(Today, 5), Day(Today.AddDays(1), 1));

        Assert.Equal([(Today.AddDays(1), true), (Today, false)], _viewModel.Days.Select(d => (d.Day, d.IsToday)));
    }

    [Fact]
    public void Midnight_OldDayMovesDown()
    {
        ConnectWithAccounts();
        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today, 5), Day(Today.AddDays(-1), 5));

        _usage.Publish(AccountsLoadState.Ready, Today.AddDays(1), Day(Today.AddDays(-1), 5), Day(Today, 5), Day(Today.AddDays(1), 1));

        Assert.Equal([Today.AddDays(1), Today, Today.AddDays(-1)], _viewModel.Days.Select(d => d.Day));
    }

    [Fact]
    public void Disconnect_ClearsDays()
    {
        ConnectWithAccounts();
        _usage.Publish(AccountsLoadState.Ready, Today, Day(Today, 5));

        _accounts.Publish(AccountsLoadState.NotAvailable, null);

        Assert.Equal((ReportsState.NoData, 0, 0, (string?)null), (_viewModel.State, _viewModel.Days.Count, _viewModel.Accounts.Count, _usage.Selected));
    }

    [Fact]
    public void SelectingOtherAccount_SelectsInModel()
    {
        ConnectWithAccounts();

        _viewModel.SelectedAccount = _viewModel.Accounts[1];

        Assert.Equal(Max, _usage.Selected);
    }

    [Fact]
    public void Rename_KeepsSelectionOnSameAccount()
    {
        ConnectWithAccounts();
        _viewModel.SelectedAccount = _viewModel.Accounts.Single(a => a.Sid == Max);

        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "maximilian", true), Account(Anna, "anna", true)));

        Assert.Equal(("maximilian", Max), (_viewModel.SelectedAccount!.DisplayName, _usage.Selected));
        Assert.Equal(["anna", "maximilian"], _viewModel.Accounts.Select(a => a.DisplayName));
    }

    [Fact]
    public void SelectedUnticked_FirstSelected()
    {
        ConnectWithAccounts();
        _viewModel.SelectedAccount = _viewModel.Accounts.Single(a => a.Sid == Max);

        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", false), Account(Anna, "anna", true)));

        Assert.Equal(Anna, _usage.Selected);
    }

    [Fact]
    public void LastUnticked_NoControlledAccounts()
    {
        ConnectWithAccounts();

        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", false), Account(Anna, "anna", false)));

        Assert.Equal((ReportsState.NoControlledAccounts, (string?)null), (_viewModel.State, _usage.Selected));
    }

    [Fact]
    public void NewAccountTicked_InsertedInOrder()
    {
        ConnectWithAccounts();

        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", true), Account(Anna, "anna", true), Account(Lena, "lena", true)));

        Assert.Equal(["anna", "lena", "max"], _viewModel.Accounts.Select(a => a.DisplayName));
    }

    [Fact]
    public void Constructor_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => new ReportsViewModel(null!, _usage, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ReportsViewModel(_accounts, null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new ReportsViewModel(_accounts, _usage, null!));
    }

    private void ConnectWithAccounts() =>
        _accounts.Publish(AccountsLoadState.Ready, Accounts(Account(Max, "max", true), Account(Anna, "anna", true)));

    private static UserAccountDto Account(string sid, string name, bool ticked, string? fullName = null) => new(sid, name, fullName, false, ticked);

    private static UserAccountListDto Accounts(params UserAccountDto[] accounts) => new(1, null, accounts);

    private static DayUsageDto Day(DateOnly day, long seconds) => new(1, null, Anna, day, Today, [new AppUsageDto(1, "Editor", seconds)]);

    private sealed class FakeAccountsModel : IUserAccountsModel
    {
        public event Action? Changed;

        public AccountsLoadState LoadState { get; private set; } = AccountsLoadState.NotAvailable;

        public UserAccountListDto? Snapshot { get; private set; }

        public void Publish(AccountsLoadState state, UserAccountListDto? snapshot)
        {
            LoadState = state;
            Snapshot = snapshot;
            Changed?.Invoke();
        }

        public Task<bool> SetParentalControlAsync(string accountSid, bool isUnderParentalControl) => Task.FromResult(true);
    }

    private sealed class FakeUsageModel : IAccountUsageModel
    {
        public event Action? Changed;

        public string? Selected { get; private set; }

        public string? SelectedAccountSid => Selected;

        public AccountsLoadState LoadState { get; private set; } = AccountsLoadState.NotAvailable;

        public DateOnly? ServiceToday { get; private set; }

        public IReadOnlyList<DayUsageDto> Days { get; private set; } = [];

        public void SelectAccount(string? accountSid) => Selected = accountSid;

        public void Publish(AccountsLoadState state, DateOnly? today, params DayUsageDto[] days)
        {
            LoadState = state;
            ServiceToday = today;
            Days = days;
            Changed?.Invoke();
        }
    }
}
