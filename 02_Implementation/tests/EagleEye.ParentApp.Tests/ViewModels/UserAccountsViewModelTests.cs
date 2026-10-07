using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class UserAccountsViewModelTests
{
    private static readonly UserAccountDto Max = new("S-1-5-21-1-2-3-1001", "max", "Max Adler", false, false);
    private static readonly UserAccountDto Anna = new("S-1-5-21-1-2-3-1002", "anna", null, false, true);
    private static readonly UserAccountDto Gaming = new("S-1-5-21-1-2-3-1003", "gaming", "Gaming", true, false);

    private readonly FakeAccountsModel _model = new();
    private readonly UserAccountsViewModel _viewModel;

    public UserAccountsViewModelTests()
    {
        _viewModel = TestSupport.InCulture("de-DE", () => new UserAccountsViewModel(_model, new ImmediateDispatcher()));
    }

    // ---------- Section state ----------

    [Theory]
    [InlineData("de-DE", "Keine Daten verfügbar")]
    [InlineData("en-US", "No data available")]
    public void NotAvailable_ShowsNoData(string culture, string expected)
    {
        var text = TestSupport.InCulture(culture, () => _viewModel.StatusText);

        Assert.Equal((AccountsSectionState.NoData, expected, true, false), (_viewModel.SectionState, text, _viewModel.HasStatus, _viewModel.IsListVisible));
    }

    [Theory]
    [InlineData("de-DE", "Wird geladen …")]
    [InlineData("en-US", "Loading …")]
    public void Loading_ShowsLoading(string culture, string expected)
    {
        _model.Publish(AccountsLoadState.Loading, null);

        Assert.Equal((AccountsSectionState.Loading, expected), (_viewModel.SectionState, TestSupport.InCulture(culture, () => _viewModel.StatusText)));
    }

    [Theory]
    [InlineData("de-DE", "Keine Nicht-Administrator-Konten vorhanden")]
    [InlineData("en-US", "No non-admin accounts available")]
    public void ReadyWithoutAccounts_ShowsNoAccounts(string culture, string expected)
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1));

        Assert.Equal((AccountsSectionState.NoAccounts, expected), (_viewModel.SectionState, TestSupport.InCulture(culture, () => _viewModel.StatusText)));
    }

    [Fact]
    public void ReadyWithAccounts_ShowsListWithoutStatus()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));

        Assert.Equal((AccountsSectionState.List, string.Empty, false, true), (_viewModel.SectionState, _viewModel.StatusText, _viewModel.HasStatus, _viewModel.IsListVisible));
    }

    [Fact]
    public void ReadyWithoutSnapshot_ShowsNoData()
    {
        _model.Publish(AccountsLoadState.Ready, null);

        Assert.Equal(AccountsSectionState.NoData, _viewModel.SectionState);
    }

    [Fact]
    public void Disconnected_ClearsList()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));

        _model.Publish(AccountsLoadState.NotAvailable, null);

        Assert.Equal((AccountsSectionState.NoData, 0), (_viewModel.SectionState, _viewModel.Accounts.Count));
    }

    // ---------- Rows ----------

    [Fact]
    public void Rows_ShowNameAndStoredValueSortedByShownName()
    {
        var rows = TestSupport.InCulture("de-DE", () =>
        {
            _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna, Gaming));
            return _viewModel.Accounts.Select(a => (a.DisplayName, a.IsUnderParentalControl, a.IsEnabled)).ToList();
        });

        Assert.Equal(
            [("anna", true, true), ("Gaming (gaming) (deaktiviert)", false, true), ("Max Adler (max)", false, true)],
            rows);
    }

    [Fact]
    public void Rows_SortIgnoresCaseAndUsesCulture()
    {
        var names = TestSupport.InCulture("de-DE", () =>
        {
            _model.Publish(AccountsLoadState.Ready, Snapshot(1,
                Account(1, "Zoe"), Account(2, "Ärger"), Account(3, "anna"), Account(4, "Max"), Account(5, "bernd")));
            return _viewModel.Accounts.Select(a => a.DisplayName).ToList();
        });

        Assert.Equal(["anna", "Ärger", "bernd", "Max", "Zoe"], names);
    }

    [Fact]
    public void Merge_KeepsItemInstancesAndUpdatesInPlace()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));
        var maxItem = _viewModel.Accounts.Single(a => a.Sid == Max.Sid);

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max with { IsUnderParentalControl = true }, Anna));

        Assert.Same(maxItem, _viewModel.Accounts.Single(a => a.Sid == Max.Sid));
        Assert.True(maxItem.IsUnderParentalControl);
    }

    [Fact]
    public void Merge_InsertsAndRemovesAtSortedPositions()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max, Account(7, "lena")));

        Assert.Equal(["lena", "Max Adler (max)"], _viewModel.Accounts.Select(a => a.DisplayName));
    }

    [Fact]
    public void Merge_RenameMovesRow()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));
        var maxItem = _viewModel.Accounts.Single(a => a.Sid == Max.Sid);

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max with { FullName = "Aaron Adler" }, Anna));

        Assert.Equal((0, "Aaron Adler (max)"), (_viewModel.Accounts.IndexOf(maxItem), maxItem.DisplayName));
    }

    [Fact]
    public void Merge_ProgrammaticUpdate_DoesNotWrite()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max with { IsUnderParentalControl = true }));

        Assert.Empty(_model.Writes);
    }

    [Fact]
    public void DisabledRow_IsToggleable()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Gaming));

        _viewModel.Accounts[0].IsUnderParentalControl = true;

        Assert.Equal([(Gaming.Sid, true)], _model.Writes.Select(w => (w.Sid, w.Value)));
    }

    // ---------- Writes ----------

    [Fact]
    public void Toggle_CallsModelOnceAndDisablesRow()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        var item = _viewModel.Accounts[0];

        item.IsUnderParentalControl = true;
        item.IsUnderParentalControl = true;

        Assert.Equal((1, false, true), (_model.Writes.Count, item.IsEnabled, item.IsUnderParentalControl));
    }

    [Fact]
    public async Task Toggle_Success_EnablesRowShowsStoredValueAndClearsError()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        var item = _viewModel.Accounts[0];
        await FailWriteAsync(item);
        item.IsUnderParentalControl = true;

        _model.Publish(AccountsLoadState.Ready, Snapshot(3, Max with { IsUnderParentalControl = true }));
        _model.Writes[^1].Result.SetResult(true);
        await _viewModel.LastWrite;

        Assert.Equal((true, true, null, false), (item.IsEnabled, item.IsUnderParentalControl, _viewModel.ErrorText, _viewModel.HasError));
    }

    [Theory]
    [InlineData("de-DE", "Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.")]
    [InlineData("en-US", "The change could not be saved. Please try again.")]
    public async Task Toggle_Failure_ShowsErrorAndRevertsToStoredValue(string culture, string expected)
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        var item = _viewModel.Accounts[0];

        var error = await TestSupport.InCulture(culture, async () =>
        {
            await FailWriteAsync(item);
            return _viewModel.ErrorText;
        });

        Assert.Equal((expected, true, false, true), (error, _viewModel.HasError, item.IsUnderParentalControl, item.IsEnabled));
    }

    [Fact]
    public void PendingRow_KeepsRequestedValueWhileForeignSnapshotArrives()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));
        var item = _viewModel.Accounts.Single(a => a.Sid == Max.Sid);
        item.IsUnderParentalControl = true;

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max, Anna with { IsUnderParentalControl = false }));

        Assert.Equal((true, false), (item.IsUnderParentalControl, _viewModel.Accounts.Single(a => a.Sid == Anna.Sid).IsUnderParentalControl));
    }

    [Fact]
    public async Task WriteCompletesAfterDisconnect_RowRemovedNothingThrows()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        var item = _viewModel.Accounts[0];
        item.IsUnderParentalControl = true;
        _model.Publish(AccountsLoadState.NotAvailable, null);

        _model.Writes[0].Result.SetResult(false);
        await _viewModel.LastWrite;

        Assert.Equal((0, true), (_viewModel.Accounts.Count, item.IsEnabled));
    }

    [Fact]
    public async Task WriteCompletesAfterAccountWasDeleted_OtherRowsUnchanged()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max, Anna));
        _viewModel.Accounts.Single(a => a.Sid == Max.Sid).IsUnderParentalControl = true;
        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Anna));

        _model.Writes[0].Result.SetResult(false);
        await _viewModel.LastWrite;

        Assert.Equal([("anna", true)], _viewModel.Accounts.Select(a => (a.DisplayName, a.IsUnderParentalControl)));
    }

    [Fact]
    public async Task Error_ClearedOnDisconnectAndOnConnect()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        await FailWriteAsync(_viewModel.Accounts[0]);

        _model.Publish(AccountsLoadState.NotAvailable, null);
        var afterDisconnect = _viewModel.ErrorText;
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        await FailWriteAsync(_viewModel.Accounts[0]);
        _model.Publish(AccountsLoadState.Loading, null);

        Assert.Equal((null, null), (afterDisconnect, _viewModel.ErrorText));
    }

    [Fact]
    public async Task Error_KeptOnFurtherSnapshots()
    {
        _model.Publish(AccountsLoadState.Ready, Snapshot(1, Max));
        await FailWriteAsync(_viewModel.Accounts[0]);

        _model.Publish(AccountsLoadState.Ready, Snapshot(2, Max));

        Assert.True(_viewModel.HasError);
    }

    [Fact]
    public void Constructor_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => new UserAccountsViewModel(null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new UserAccountsViewModel(_model, null!));
    }

    // ---------- Helpers ----------

    private async Task FailWriteAsync(UserAccountItemViewModel item)
    {
        item.IsUnderParentalControl = !item.IsUnderParentalControl;
        _model.Writes[^1].Result.SetResult(false);
        await _viewModel.LastWrite;
    }

    private static UserAccountDto Account(int rid, string userName) => new($"S-1-5-21-1-2-3-{2000 + rid}", userName, null, false, false);

    private static UserAccountListDto Snapshot(long revision, params UserAccountDto[] accounts) => new(revision, null, accounts);

    private sealed class FakeAccountsModel : IUserAccountsModel
    {
        public event Action? Changed;

        public AccountsLoadState LoadState { get; private set; } = AccountsLoadState.NotAvailable;

        public UserAccountListDto? Snapshot { get; private set; }

        public List<(string Sid, bool Value, TaskCompletionSource<bool> Result)> Writes { get; } = [];

        public void Publish(AccountsLoadState state, UserAccountListDto? snapshot)
        {
            LoadState = state;
            Snapshot = snapshot;
            Changed?.Invoke();
        }

        public Task<bool> SetParentalControlAsync(string accountSid, bool isUnderParentalControl)
        {
            var result = new TaskCompletionSource<bool>();
            Writes.Add((accountSid, isUnderParentalControl, result));
            return result.Task;
        }
    }
}
