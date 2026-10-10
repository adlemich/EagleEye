using System.ComponentModel;
using EagleEye.Service.Data;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.UserAccounts;

public sealed class UserAccountServiceTests : IAsyncLifetime
{
    private const string Machine = "S-1-5-21-1111111111-2222222222-3333333333";
    private const string Device = "Dad's laptop";

    private static readonly LocalAccountInfo Administrator = new($"{Machine}-500", "Administrator", null, true, true);
    private static readonly LocalAccountInfo Guest = new($"{Machine}-501", "Gast", null, true, false);
    private static readonly LocalAccountInfo Papa = new($"{Machine}-1000", "papa", "Michael Adler", false, true);
    private static readonly LocalAccountInfo Max = new($"{Machine}-1001", "max", "Max Adler", false, false);
    private static readonly LocalAccountInfo Anna = new($"{Machine}-1002", "anna", null, false, false);
    private static readonly LocalAccountInfo Leftover = new($"{Machine}-1003", "defaultuser0", null, false, false);

    private static readonly Guid RequestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RequestB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AccountSelectionRepository _repository;
    private readonly Mock<ILocalAccountSource> _source = new();
    private readonly RecordingBroadcaster _broadcaster = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly TestLogger<UserAccountService> _logger = new();
    private readonly Mock<IAccountDataPurger> _purger = new();
    private readonly Mock<IAccountDataPurger> _rulesPurger = new();
    private List<LocalAccountInfo> _accounts = [Administrator, Guest, Papa, Max, Anna, Leftover];
    private UserAccountService _service;

    public UserAccountServiceTests()
    {
        _repository = new AccountSelectionRepository(_database);
        _source.Setup(s => s.GetAccounts()).Returns(() => _accounts.ToList());
        _service = CreateService(_repository);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    // ---------- Initialize / snapshot ----------

    [Fact]
    public async Task InitializeAsync_SnapshotHasRevision1AndOnlyStandardAccountsOrderedByUserName()
    {
        await _service.InitializeAsync();

        var snapshot = await _service.GetSnapshotAsync();

        Assert.Equal((1L, (Guid?)null), (snapshot.Revision, snapshot.LastChangeRequestId));
        Assert.Equal(
            [new UserAccountDto(Anna.Sid, "anna", null, false, false), new UserAccountDto(Max.Sid, "max", "Max Adler", false, false)],
            snapshot.Accounts);
    }

    [Fact]
    public async Task InitializeAsync_LoadsStoredSelections()
    {
        await _repository.SetAsync(Max.Sid, "max", true, _time.GetUtcNow());

        await _service.InitializeAsync();

        Assert.True(await IsTickedAsync(Max));
    }

    [Fact]
    public async Task InitializeAsync_LogsCounts()
    {
        await _repository.SetAsync(Max.Sid, "max", true, _time.GetUtcNow());

        await _service.InitializeAsync();

        Assert.Contains("Account inventory loaded: 2 standard accounts, 1 under parental control.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task InitializeAsync_AccountDeletedWhileServiceStopped_ForgetsSelection()
    {
        await _repository.SetAsync($"{Machine}-1099", "gone", true, _time.GetUtcNow());

        await _service.InitializeAsync();

        Assert.Empty(await _repository.LoadAllAsync());
        Assert.Contains($"Forgot the parental-control selection of 1 deleted account(s): {Machine}-1099.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task InitializeAsync_EnumerationFails_LogsErrorAndSnapshotUnavailable()
    {
        var failure = new Win32Exception(5);
        _source.Setup(s => s.GetAccounts()).Throws(failure);

        await _service.InitializeAsync();

        Assert.True(_logger.Has(LogLevel.Error, failure));
        await Assert.ThrowsAsync<AccountInventoryUnavailableException>(() => _service.GetSnapshotAsync());
    }

    [Fact]
    public async Task InitializeAsync_NoAccounts_SnapshotUnavailableAndNothingDeleted()
    {
        await _repository.SetAsync(Max.Sid, "max", true, _time.GetUtcNow());
        _accounts = [];

        await _service.InitializeAsync();

        Assert.Single(await _repository.LoadAllAsync());
        await Assert.ThrowsAsync<AccountInventoryUnavailableException>(() => _service.GetSnapshotAsync());
    }

    [Fact]
    public async Task InitializeAsync_DoesNotBroadcast()
    {
        await _service.InitializeAsync();

        Assert.Empty(_broadcaster.Snapshots);
    }

    // ---------- SetParentalControl ----------

    [Fact]
    public async Task SetParentalControlAsync_Stores()
    {
        await _service.InitializeAsync();

        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);

        Assert.True((await _repository.LoadAllAsync())[Max.Sid]);
    }

    [Fact]
    public async Task SetParentalControlAsync_IncrementsRevisionAndReturnsIt()
    {
        await _service.InitializeAsync();

        var first = await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        var second = await _service.SetParentalControlAsync(RequestB, Anna.Sid, true, Device);

        Assert.Equal((2L, 3L), (first.Revision, second.Revision));
    }

    [Fact]
    public async Task SetParentalControlAsync_BroadcastsSnapshotWithRequestIdBeforeReturning()
    {
        await _service.InitializeAsync();

        var ack = await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);

        var broadcast = Assert.Single(_broadcaster.Snapshots);
        Assert.Equal((ack.Revision, (Guid?)RequestA, true), (broadcast.Revision, broadcast.LastChangeRequestId, Ticked(broadcast, Max)));
    }

    [Fact]
    public async Task SetParentalControlAsync_LaterQueryCarriesRequestId()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);

        Assert.Equal(RequestA, (await _service.GetSnapshotAsync()).LastChangeRequestId);
    }

    [Theory]
    [InlineData(true, "yes")]
    [InlineData(false, "no")]
    public async Task SetParentalControlAsync_LogsUserNameAndState(bool value, string text)
    {
        await _service.InitializeAsync();

        await _service.SetParentalControlAsync(RequestA, Max.Sid, value, Device);

        Assert.Contains(
            $"Account max ({Max.Sid}): under parental control = {text} (set by parent device {Device}, request {RequestA}, revision 2).",
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task SetParentalControlAsync_UnchangedValue_StillNewRevisionAndBroadcast()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);

        var ack = await _service.SetParentalControlAsync(RequestB, Max.Sid, true, Device);

        Assert.Equal((3L, 2), (ack.Revision, _broadcaster.Snapshots.Count));
    }

    [Fact]
    public async Task SetParentalControlAsync_SidInOtherCase_IsAccepted()
    {
        await _service.InitializeAsync();

        await _service.SetParentalControlAsync(RequestA, Max.Sid.ToLowerInvariant(), true, Device);

        Assert.True(await IsTickedAsync(Max));
    }

    [Theory]
    [InlineData(Machine + "-1000")] // admin (papa)
    [InlineData(Machine + "-501")] // guest
    [InlineData(Machine + "-1003")] // defaultuser0
    [InlineData(Machine + "-1099")] // does not exist
    public async Task SetParentalControlAsync_NotAStandardAccount_ThrowsUnknownAccountAndStoresNothing(string sid)
    {
        await _service.InitializeAsync();

        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.SetParentalControlAsync(RequestA, sid, true, Device));

        Assert.Equal((0, 0, 1L), ((await _repository.LoadAllAsync()).Count, _broadcaster.Snapshots.Count, (await _service.GetSnapshotAsync()).Revision));
        Assert.Contains($"Setting parental control for {sid} rejected: unknown account.", _logger.Messages(LogLevel.Warning));
    }

    [Fact]
    public async Task SetParentalControlAsync_InventoryUnavailable_Throws()
    {
        _source.Setup(s => s.GetAccounts()).Throws(new Win32Exception(5));
        await _service.InitializeAsync();

        await Assert.ThrowsAsync<AccountInventoryUnavailableException>(() => _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device));
    }

    [Fact]
    public async Task SetParentalControlAsync_RepositoryFails_PropagatesWithoutRevisionOrBroadcast()
    {
        var repository = new Mock<IAccountSelectionRepository>();
        repository.Setup(r => r.LoadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, bool>());
        repository.Setup(r => r.DeleteMissingAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk"));
        _service = CreateService(repository.Object);
        await _service.InitializeAsync();

        await Assert.ThrowsAsync<IOException>(() => _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device));

        Assert.Equal((1L, 0, false), ((await _service.GetSnapshotAsync()).Revision, _broadcaster.Snapshots.Count, await IsTickedAsync(Max)));
    }

    [Fact]
    public async Task SetParentalControlAsync_BroadcastFails_LogsWarningAndWriteSucceeds()
    {
        var failure = new IOException("connection reset");
        _broadcaster.Failure = failure;
        await _service.InitializeAsync();

        var ack = await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);

        Assert.Equal((2L, true), (ack.Revision, _logger.Has(LogLevel.Warning, failure)));
        Assert.True(await IsTickedAsync(Max));
    }

    [Fact]
    public async Task SetParentalControlAsync_Concurrent_SerializedInOrderLastWins()
    {
        await _service.InitializeAsync();
        var release = new TaskCompletionSource();
        _broadcaster.Gate = release.Task;

        var first = _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        var second = _service.SetParentalControlAsync(RequestB, Max.Sid, false, Device);
        release.SetResult();
        var acks = await Task.WhenAll(first, second);

        Assert.Equal([2L, 3L], acks.Select(a => a.Revision));
        Assert.Equal([(2L, (Guid?)RequestA, true), (3L, RequestB, false)], _broadcaster.Snapshots.Select(s => (s.Revision, s.LastChangeRequestId, Ticked(s, Max))));
        Assert.False(await IsTickedAsync(Max));
    }

    [Fact]
    public async Task SetParentalControlAsync_Guards_ThrowArgumentException()
    {
        await _service.InitializeAsync();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SetParentalControlAsync(RequestA, " ", true, Device));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetParentalControlAsync(RequestA, Max.Sid, true, null!));
    }

    // ---------- RefreshInventory ----------

    [Fact]
    public async Task RefreshInventoryAsync_NoChange_NoBroadcast()
    {
        await _service.InitializeAsync();

        await _service.RefreshInventoryAsync();

        Assert.Equal((0, 1L), (_broadcaster.Snapshots.Count, (await _service.GetSnapshotAsync()).Revision));
    }

    public static TheoryData<string, LocalAccountInfo[]> StandardChanges => new()
    {
        { "added", [Papa, Max, Anna, new($"{Machine}-1004", "lena", null, false, false)] },
        { "deleted", [Papa, Max] },
        { "renamed", [Papa, Max with { UserName = "maximilian" }, Anna] },
        { "full name", [Papa, Max with { FullName = "Maximilian Adler" }, Anna] },
        { "disabled", [Papa, Max with { IsDisabled = true }, Anna] },
        { "became admin", [Papa, Max with { IsAdmin = true }, Anna] },
        { "admin became standard", [Papa with { IsAdmin = false }, Max, Anna] },
    };

    [Theory]
    [MemberData(nameof(StandardChanges))]
    public async Task RefreshInventoryAsync_StandardAccountsChanged_OneBroadcastWithoutRequestId(string change, LocalAccountInfo[] accounts)
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _broadcaster.Snapshots.Clear();
        _accounts = [.. accounts];

        await _service.RefreshInventoryAsync();

        var broadcast = Assert.Single(_broadcaster.Snapshots);
        Assert.Equal((3L, (Guid?)null), (broadcast.Revision, broadcast.LastChangeRequestId));
        Assert.Equal(AccountInventoryFilter.Standard(accounts).Count, broadcast.Accounts.Count);
        _ = change;
    }

    [Fact]
    public async Task RefreshInventoryAsync_Renamed_KeepsSelectionAndShowsNewName()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _accounts = [Papa, Max with { UserName = "maximilian" }, Anna];

        await _service.RefreshInventoryAsync();

        var account = (await _service.GetSnapshotAsync()).Accounts.Single(a => a.Sid == Max.Sid);
        Assert.Equal(("maximilian", true), (account.UserName, account.IsUnderParentalControl));
    }

    [Fact]
    public async Task RefreshInventoryAsync_LogsDifference()
    {
        await _service.InitializeAsync();
        _accounts = [Papa, Max with { UserName = "maximilian" }, new($"{Machine}-1004", "lena", null, false, false)];

        await _service.RefreshInventoryAsync();

        Assert.Contains(
            "Account inventory changed (revision 2): added [lena], removed [anna], changed [maximilian]; 2 standard accounts.",
            _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task RefreshInventoryAsync_BecameAdmin_NotInSnapshotRowKeptWriteRejected()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _accounts = [Papa, Max with { IsAdmin = true }, Anna];

        await _service.RefreshInventoryAsync();

        Assert.DoesNotContain((await _service.GetSnapshotAsync()).Accounts, a => a.Sid == Max.Sid);
        Assert.True((await _repository.LoadAllAsync())[Max.Sid]);
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.SetParentalControlAsync(RequestB, Max.Sid, false, Device));
    }

    [Fact]
    public async Task RefreshInventoryAsync_BackToStandard_SelectionRestored()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _accounts = [Papa, Max with { IsAdmin = true }, Anna];
        await _service.RefreshInventoryAsync();
        _accounts = [Papa, Max, Anna];

        await _service.RefreshInventoryAsync();

        Assert.True(await IsTickedAsync(Max));
    }

    [Fact]
    public async Task RefreshInventoryAsync_Deleted_RowDeletedAndNewAccountWithSameNameUnticked()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _accounts = [Papa, Anna];
        await _service.RefreshInventoryAsync();
        var newMax = Max with { Sid = $"{Machine}-1005" };
        _accounts = [Papa, Anna, newMax];

        await _service.RefreshInventoryAsync();

        Assert.Empty(await _repository.LoadAllAsync());
        Assert.False(await IsTickedAsync(newMax));
    }

    [Fact]
    public async Task RefreshInventoryAsync_AdminWithSelectionDeleted_RowDeletedWithoutBroadcast()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _accounts = [Papa, Max with { IsAdmin = true }, Anna];
        await _service.RefreshInventoryAsync();
        _broadcaster.Snapshots.Clear();
        _accounts = [Papa, Anna];

        await _service.RefreshInventoryAsync();

        Assert.Equal((0, 0), ((await _repository.LoadAllAsync()).Count, _broadcaster.Snapshots.Count));
    }

    [Fact]
    public async Task RefreshInventoryAsync_EnumerationFails_NothingChangedOrDeletedAndWarning()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _broadcaster.Snapshots.Clear();
        var failure = new Win32Exception(1722);
        _source.Setup(s => s.GetAccounts()).Throws(failure);

        await _service.RefreshInventoryAsync();

        Assert.Equal((true, 0, 2L), (_logger.Has(LogLevel.Warning, failure), _broadcaster.Snapshots.Count, (await _service.GetSnapshotAsync()).Revision));
        Assert.True((await _repository.LoadAllAsync())[Max.Sid]);
    }

    [Fact]
    public async Task RefreshInventoryAsync_NoAccounts_NothingDeletedNoBroadcast()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        _broadcaster.Snapshots.Clear();
        _accounts = [];

        await _service.RefreshInventoryAsync();

        Assert.Equal((1, 0), ((await _repository.LoadAllAsync()).Count, _broadcaster.Snapshots.Count));
        Assert.Contains("Windows returned no local accounts; the inventory is kept unchanged.", _logger.Messages(LogLevel.Warning));
    }

    [Fact]
    public async Task RefreshInventoryAsync_UnavailableBefore_BecomesAvailableAndBroadcasts()
    {
        _source.Setup(s => s.GetAccounts()).Throws(new Win32Exception(5));
        await _service.InitializeAsync();
        _source.Setup(s => s.GetAccounts()).Returns(() => _accounts.ToList());

        await _service.RefreshInventoryAsync();

        Assert.Equal((2L, 2), (Assert.Single(_broadcaster.Snapshots).Revision, (await _service.GetSnapshotAsync()).Accounts.Count));
    }

    [Fact]
    public async Task RefreshInventoryAsync_NewAccount_IsUnticked()
    {
        await _service.InitializeAsync();
        var lena = new LocalAccountInfo($"{Machine}-1004", "lena", null, false, false);
        _accounts = [Papa, Max, Anna, lena];

        await _service.RefreshInventoryAsync();

        Assert.False(await IsTickedAsync(lena));
    }

    // ---------- US-004: controlled accounts and purge hook ----------

    [Fact]
    public async Task GetControlledAccountsAsync_TickedStandardAccountsOnlyByName()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        await _service.SetParentalControlAsync(RequestA, Anna.Sid, true, Device);

        Assert.Equal([new ControlledAccount(Anna.Sid, "anna"), new ControlledAccount(Max.Sid, "max")], await _service.GetControlledAccountsAsync());
    }

    [Fact]
    public async Task GetControlledAccountsAsync_UntickedAndAdminExcluded()
    {
        await _service.InitializeAsync();
        await _service.SetParentalControlAsync(RequestA, Max.Sid, true, Device);
        await _service.SetParentalControlAsync(RequestA, Anna.Sid, false, Device);
        _accounts = [Papa, Max with { IsAdmin = true }, Anna];
        await _service.RefreshInventoryAsync();

        Assert.Empty(await _service.GetControlledAccountsAsync());
    }

    [Fact]
    public async Task GetControlledAccountsAsync_InventoryUnavailable_Empty()
    {
        _source.Setup(s => s.GetAccounts()).Throws(new Win32Exception(5));
        await _service.InitializeAsync();

        Assert.Empty(await _service.GetControlledAccountsAsync());
    }

    [Fact]
    public async Task InitializeAsync_CallsEveryPurgerWithAllSids()
    {
        await _service.InitializeAsync();

        _purger.Verify(p => p.PurgeMissingAccountsAsync(
            It.Is<IReadOnlyCollection<string>>(sids => sids.Count == 6 && sids.Contains(Papa.Sid)), It.IsAny<CancellationToken>()), Times.Once);
        _rulesPurger.Verify(p => p.PurgeMissingAccountsAsync(
            It.Is<IReadOnlyCollection<string>>(sids => sids.Count == 6), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshInventoryAsync_Changed_PurgesUsage()
    {
        await _service.InitializeAsync();
        _accounts = [Papa, Max];

        await _service.RefreshInventoryAsync();

        _purger.Verify(p => p.PurgeMissingAccountsAsync(
            It.Is<IReadOnlyCollection<string>>(sids => sids.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshInventoryAsync_EnumerationFails_NoPurge()
    {
        await _service.InitializeAsync();
        _purger.Invocations.Clear();
        _source.Setup(s => s.GetAccounts()).Throws(new Win32Exception(5));

        await _service.RefreshInventoryAsync();

        _purger.VerifyNoOtherCalls();
        _rulesPurger.Verify(p => p.PurgeMissingAccountsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Helpers ----------

    private UserAccountService CreateService(IAccountSelectionRepository repository)
    {
        return new UserAccountService(_source.Object, repository, _broadcaster, _time, new Lazy<IEnumerable<IAccountDataPurger>>([_purger.Object, _rulesPurger.Object]), _logger);
    }

    private async Task<bool> IsTickedAsync(LocalAccountInfo account)
    {
        return Ticked(await _service.GetSnapshotAsync(), account);
    }

    private static bool Ticked(UserAccountListDto snapshot, LocalAccountInfo account)
    {
        return snapshot.Accounts.Single(a => a.Sid == account.Sid).IsUnderParentalControl;
    }

    private sealed class RecordingBroadcaster : IUserAccountsBroadcaster
    {
        public List<UserAccountListDto> Snapshots { get; } = [];

        public Exception? Failure { get; set; }

        public Task Gate { get; set; } = Task.CompletedTask;

        public async Task BroadcastAsync(UserAccountListDto snapshot)
        {
            await Gate;
            Snapshots.Add(snapshot);
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
