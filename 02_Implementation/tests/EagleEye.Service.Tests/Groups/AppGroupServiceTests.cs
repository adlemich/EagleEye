using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Groups;
using EagleEye.Service.Rules;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class AppGroupServiceTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Device = "Dad's MacBook";
    private const string Notepad = @"C:\Apps\notepad.exe";
    private const string Paint = @"C:\Apps\mspaint.exe";
    private const string Explorer = @"C:\Windows\explorer.exe";
    private static readonly Guid RequestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RequestB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AppGroupRepository _repository;
    private readonly UsageRepository _usage;
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly Mock<IAccountRulesBroadcaster> _broadcaster = new();
    private readonly List<AppGroupsDto> _groupBroadcasts = [];
    private readonly List<AppGroupRulesDto> _ruleBroadcasts = [];
    private readonly FakeTimeProvider _time = new(Now);
    private readonly TestLogger<AppGroupService> _logger = new();
    private AppGroupService _service;

    public AppGroupServiceTests()
    {
        _repository = new AppGroupRepository(_database);
        _usage = new UsageRepository(_database);
        _accounts.Setup(a => a.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserAccountListDto(1, null,
        [
            new UserAccountDto(Kid, "kid1", null, false, true),
            new UserAccountDto(Kid2, "kid2", null, false, true),
        ]));
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AppGroupsDto>())).Callback<AppGroupsDto>(_groupBroadcasts.Add).Returns(Task.CompletedTask);
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AppGroupRulesDto>())).Callback<AppGroupRulesDto>(_ruleBroadcasts.Add).Returns(Task.CompletedTask);
        _service = Create(_repository);
    }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _usage.GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Now);
        await _usage.GetOrCreateAppAsync(Kid2, Notepad.ToUpperInvariant(), "notepad.exe", "Notepad", Now.AddMinutes(1));
        await _usage.GetOrCreateAppAsync(Kid, Explorer, "explorer.exe", "Windows-Explorer", Now);
        await _service.InitializeAsync();
        await _service.OnControlledAccountsAsync([Kid, Kid2]);
        _groupBroadcasts.Clear();
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _database.DisposeAsync();
    }

    // ---------- Initialize / catalog ----------

    [Fact]
    public async Task Initialize_LogsAndListsEachPathOnceWithTheLatestName_IgnoredLeftOut()
    {
        var dto = await _service.GetAsync();

        var app = Assert.Single(dto.Apps);
        Assert.Equal((Notepad.ToUpperInvariant(), "Notepad", (long?)null), (app.ProgramPath, app.DisplayName, app.GroupId));
        Assert.Contains("App groups loaded: 0 groups with 0 apps, 0 group rules.", _logger.Messages(LogLevel.Information));
    }

    [Fact]
    public async Task OnControlledAccounts_UncontrolledOnly_NotListed_ChangeBroadcastOnce()
    {
        await _service.OnControlledAccountsAsync([]);
        await _service.OnControlledAccountsAsync([]);

        Assert.Empty((await _service.GetAsync()).Apps);
        Assert.Single(_groupBroadcasts);
    }

    [Fact]
    public async Task OnAppRecord_NewPath_BroadcastWithoutLog_KnownPathAndIgnoredNone()
    {
        await _service.OnAppRecordAsync(Kid, 9, Paint, "Paint");
        await _service.OnAppRecordAsync(Kid, 9, Paint, "Paint");
        await _service.OnAppRecordAsync(Kid, 3, Explorer, "Explorer");

        Assert.Single(_groupBroadcasts);
        Assert.Equal(2, _groupBroadcasts[0].Apps.Count);
        Assert.DoesNotContain(_logger.Messages(LogLevel.Information), m => m.StartsWith("App groups changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnAppRecord_NewNameOfTheLatestRecord_Broadcast()
    {
        _time.Advance(TimeSpan.FromMinutes(2));
        await _service.OnAppRecordAsync(Kid, 1, Notepad, "Editor (neu)");

        Assert.Equal("Editor (neu)", _groupBroadcasts.Single().Apps.Single().DisplayName);
    }

    // ---------- Create / rename / delete ----------

    [Fact]
    public async Task Create_StoresBroadcastsGroupsAndRulesOfEveryAccountAndLogs()
    {
        var ack = await _service.CreateAsync(RequestA, "  Games ", Device);

        var groups = _groupBroadcasts.Single();
        Assert.Equal((ack.Revision, (Guid?)RequestA, "Games"), (groups.Revision, groups.LastChangeRequestId, groups.Groups.Single().Name));
        Assert.Equal([Kid, Kid2], _ruleBroadcasts.Select(r => r.AccountSid));
        Assert.All(_ruleBroadcasts, r => Assert.Equal(new AppGroupRuleDto(groups.Groups[0].GroupId, 0, BreakTimeDays.None), r.Rules.Single()));
        Assert.Contains(
            $"App groups changed by {Device}: app group created: Games (request {RequestA}, revision {ack.Revision}).", _logger.Messages(LogLevel.Information));
        Assert.Single((await _repository.LoadAllAsync()).Groups);
    }

    [Theory]
    [InlineData("games")]
    [InlineData("GAMES ")]
    public async Task Create_DuplicateIgnoringCase_Throws(string name)
    {
        await _service.CreateAsync(RequestA, "Games", Device);

        await Assert.ThrowsAsync<DuplicateGroupNameException>(() => _service.CreateAsync(RequestB, name, Device));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_InvalidName_Throws(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(RequestA, name, Device));
    }

    [Fact]
    public async Task Rename_ToOwnRecasedName_Valid_MembersAndRulesKept()
    {
        var id = await CreateAsync("Games");
        await _service.MoveAsync(RequestA, Notepad, id, Device);
        await _service.SetBudgetAsync(RequestA, Kid, id, AppBudgetPart.Hours, 1, Device);
        _ruleBroadcasts.Clear();

        await _service.RenameAsync(RequestB, id, "GAMES", Device);

        Assert.Equal("GAMES", _service.Current.Names[id]);
        Assert.Equal((id, 60), (_service.Current.GroupOf(Notepad)!.Value, _service.Current.RuleFor(id, Kid).BudgetMinutes));
        Assert.Empty(_ruleBroadcasts);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("app group renamed: Games → GAMES", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rename_ToAnotherGroupsName_Throws_UnknownGroup_Throws()
    {
        var id = await CreateAsync("Games");
        await CreateAsync("School");

        await Assert.ThrowsAsync<DuplicateGroupNameException>(() => _service.RenameAsync(RequestA, id, "school", Device));
        await Assert.ThrowsAsync<GroupNotFoundException>(() => _service.RenameAsync(RequestA, 99, "X", Device));
    }

    [Fact]
    public async Task Delete_RemovesGroupMembersAndRules_BroadcastsRulesOfEveryAccount()
    {
        var id = await CreateAsync("Games");
        await _service.MoveAsync(RequestA, Notepad, id, Device);
        await _service.SetBudgetAsync(RequestA, Kid, id, AppBudgetPart.Hours, 1, Device);
        _ruleBroadcasts.Clear();

        await _service.DeleteAsync(RequestB, id, Device);

        Assert.Empty(_service.Current.Names);
        Assert.Null(_service.Current.GroupOf(Notepad));
        Assert.Equal(2, _ruleBroadcasts.Count);
        Assert.All(_ruleBroadcasts, r => Assert.Empty(r.Rules));
        Assert.Null(_groupBroadcasts.Last().Apps.Single().GroupId);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("app group deleted: Games", StringComparison.Ordinal));
        await Assert.ThrowsAsync<GroupNotFoundException>(() => _service.DeleteAsync(RequestB, id, Device));
    }

    [Fact]
    public async Task RepositoryReportsGone_GroupNotFound_SnapshotUnchanged()
    {
        var repository = new Mock<IAppGroupRepository>();
        repository.Setup(r => r.LoadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredAppGroups([new StoredAppGroup(1, "Games")], [], [], []));
        using var service = Create(repository.Object);
        await service.InitializeAsync();

        await Assert.ThrowsAsync<GroupNotFoundException>(() => service.RenameAsync(RequestA, 1, "X", Device));
        await Assert.ThrowsAsync<GroupNotFoundException>(() => service.DeleteAsync(RequestA, 1, Device));

        Assert.Equal("Games", service.Current.Names[1]);
    }

    [Fact]
    public async Task StorageFailure_SnapshotAndRevisionUnchanged()
    {
        var repository = new Mock<IAppGroupRepository>();
        repository.Setup(r => r.LoadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StoredAppGroups([], [], [], []));
        repository.Setup(r => r.CreateGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));
        using var service = Create(repository.Object);
        await service.InitializeAsync();
        var before = (await service.GetAsync()).Revision;

        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(RequestA, "Games", Device));

        Assert.Equal((before, 0), ((await service.GetAsync()).Revision, service.Current.Names.Count));
    }

    // ---------- Move ----------

    [Fact]
    public async Task Move_IntoGroupThenAnotherThenOut_OneSectionAlways()
    {
        var games = await CreateAsync("Games");
        var school = await CreateAsync("School");

        await _service.MoveAsync(RequestA, Notepad.ToLowerInvariant(), games, Device);
        await _service.MoveAsync(RequestA, Notepad, school, Device);
        Assert.Equal(school, (await _service.GetAsync()).Apps.Single().GroupId);

        await _service.MoveAsync(RequestB, Notepad, null, Device);

        Assert.Null((await _service.GetAsync()).Apps.Single().GroupId);
        Assert.Empty((await _repository.LoadAllAsync()).Members);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("app moved: Notepad → School", StringComparison.Ordinal));
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("app moved: Notepad → not in any group", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Move_NotListedOrUnknownGroup_Throws()
    {
        var games = await CreateAsync("Games");

        await Assert.ThrowsAsync<AppNotFoundException>(() => _service.MoveAsync(RequestA, Paint, games, Device));
        await Assert.ThrowsAsync<GroupNotFoundException>(() => _service.MoveAsync(RequestA, Notepad, 99, Device));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.MoveAsync(RequestA, " ", games, Device));
    }

    [Fact]
    public async Task Move_NoOp_StillOneRevisionAndBroadcast()
    {
        _groupBroadcasts.Clear();

        var first = await _service.MoveAsync(RequestA, Notepad, null, Device);
        var second = await _service.MoveAsync(RequestB, Notepad, null, Device);

        Assert.Equal((first.Revision + 1, 2), (second.Revision, _groupBroadcasts.Count));
    }

    [Fact]
    public async Task Membership_SurvivesUntick_NotListedButKept()
    {
        var games = await CreateAsync("Games");
        await _service.MoveAsync(RequestA, Notepad, games, Device);

        await _service.OnControlledAccountsAsync([]);

        Assert.Empty((await _service.GetAsync()).Apps);
        Assert.Equal(games, _service.Current.GroupOf(Notepad));
    }

    // ---------- Group rules ----------

    [Fact]
    public async Task GetRules_RowForEveryGroup_DefaultWhenNothingStored()
    {
        var games = await CreateAsync("Games");
        await _service.SetDayAsync(RequestA, Kid, games, DayOfWeek.Saturday, true, Device);

        var kid1 = await _service.GetRulesAsync(Kid.ToLowerInvariant());
        var kid2 = await _service.GetRulesAsync(Kid2);

        Assert.Equal(new AppGroupRuleDto(games, 0, BreakTimeDays.Saturday), kid1.Rules.Single());
        Assert.Equal((RequestA, new AppGroupRuleDto(games, 0, BreakTimeDays.None)), (kid1.LastChangeRequestId!.Value, kid2.Rules.Single()));
        Assert.Null(kid2.LastChangeRequestId);
    }

    [Fact]
    public async Task SetBudget_CombinesHoursAndMinutes_LogsAndBroadcasts()
    {
        var games = await CreateAsync("Games");
        _ruleBroadcasts.Clear();

        await _service.SetBudgetAsync(RequestA, Kid, games, AppBudgetPart.Hours, 1, Device);
        await _service.SetBudgetAsync(RequestA, Kid, games, AppBudgetPart.Minutes, 30, Device);
        await _service.SetDayAsync(RequestB, Kid, games, DayOfWeek.Sunday, true, Device);
        await _service.SetDayAsync(RequestB, Kid, games, DayOfWeek.Saturday, true, Device);
        var ack = await _service.SetDayAsync(RequestB, Kid, games, DayOfWeek.Sunday, false, Device);

        Assert.Equal(new LimitRule(90, BreakTimeDays.Saturday), _service.Current.RuleFor(games, Kid));
        Assert.Equal(5, _ruleBroadcasts.Count);
        Assert.Contains(
            $"Group rules of account kid1 ({Kid}) changed by {Device}: group rule changed: Games, 1:30, Sa (request {RequestB}, revision {ack.Revision}).",
            _logger.Messages(LogLevel.Information));
        Assert.Equal(90, (await _repository.LoadAllAsync()).Rules.Single().BudgetMinutes);
    }

    [Fact]
    public async Task SetRule_BackToDefault_RowDeleted()
    {
        var games = await CreateAsync("Games");
        await _service.SetDayAsync(RequestA, Kid, games, DayOfWeek.Monday, true, Device);

        await _service.SetDayAsync(RequestA, Kid, games, DayOfWeek.Monday, false, Device);

        Assert.Empty((await _repository.LoadAllAsync()).Rules);
        Assert.Contains(_logger.Messages(LogLevel.Information), m => m.Contains("group rule changed: Games, 0:00, no day", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetRule_UnknownAccountGroupOrPart_Throws()
    {
        var games = await CreateAsync("Games");

        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.SetDayAsync(RequestA, "S-1-5-21-9", games, DayOfWeek.Monday, true, Device));
        await Assert.ThrowsAsync<GroupNotFoundException>(() => _service.SetDayAsync(RequestA, Kid, 99, DayOfWeek.Monday, true, Device));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SetBudgetAsync(RequestA, Kid, games, AppBudgetPart.Hours, 24, Device));
        await Assert.ThrowsAsync<UnknownAccountException>(() => _service.GetRulesAsync("S-1-5-21-9"));
    }

    // ---------- Purge / broadcast failure / guards ----------

    [Fact]
    public async Task Purge_DropsRulesAndRecordsOfDeletedAccounts_GroupsAndMembersStay()
    {
        var games = await CreateAsync("Games");
        await _service.MoveAsync(RequestA, Notepad, games, Device);
        await _service.SetBudgetAsync(RequestA, Kid, games, AppBudgetPart.Hours, 1, Device);
        await _service.SetBudgetAsync(RequestA, Kid2, games, AppBudgetPart.Hours, 2, Device);
        _groupBroadcasts.Clear();

        await _service.PurgeMissingAccountsAsync([Kid]);

        Assert.Equal((60, LimitRule.Default), (_service.Current.RuleFor(games, Kid).BudgetMinutes, _service.Current.RuleFor(games, Kid2)));
        Assert.Equal((games, 1), (_service.Current.GroupOf(Notepad)!.Value, (await _repository.LoadAllAsync()).Rules.Count));
        Assert.Contains($"Purged the group rules of deleted account {Kid2}.", _logger.Messages(LogLevel.Information));
        Assert.Equal("Editor", _groupBroadcasts.Single().Apps.Single().DisplayName);
    }

    [Fact]
    public async Task BroadcastFails_WarningAndStillStored()
    {
        _broadcaster.Setup(b => b.BroadcastAsync(It.IsAny<AppGroupsDto>())).ThrowsAsync(new InvalidOperationException("hub"));

        await _service.CreateAsync(RequestA, "Games", Device);

        Assert.Single(_service.Current.Names);
        Assert.Contains(_logger.Messages(LogLevel.Warning), m => m.StartsWith("Broadcasting the app groups (AppGroups", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(RequestA, null!, Device));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(RequestA, "Games", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetDayAsync(RequestA, Kid, 1, DayOfWeek.Monday, true, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.OnControlledAccountsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.PurgeMissingAccountsAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.OnAppRecordAsync(" ", 1, Paint, "Paint"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.OnAppRecordAsync(Kid, 1, " ", "Paint"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.OnAppRecordAsync(Kid, 1, Paint, null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.GetRulesAsync(" "));
    }

    private async Task<long> CreateAsync(string name)
    {
        await _service.CreateAsync(RequestA, name, Device);
        return _service.Current.Names.Single(n => n.Value == name).Key;
    }

    private AppGroupService Create(IAppGroupRepository repository) =>
        new(repository, _broadcaster.Object, _accounts.Object, new EnforcementIgnoreList(@"C:\Windows"), _time, _logger);
}
