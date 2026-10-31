using System.ComponentModel;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Groups;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class AppGroupsViewModelTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string PaintPath = @"C:\Windows\System32\mspaint.exe";
    private const string LauncherPath = @"C:\Games\launcher.exe";
    private const string EditorPath = @"C:\Windows\notepad.exe";

    private readonly Mock<IUserAccountsModel> _accounts = new();
    private readonly Mock<IAppGroupsModel> _groups = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private AccountsLoadState _accountsState = AccountsLoadState.Ready;
    private bool _controlled = true;
    private AccountsLoadState _groupsState = AccountsLoadState.Ready;
    private AppGroupsDto? _snapshot;

    public AppGroupsViewModelTests()
    {
        _accounts.SetupGet(a => a.LoadState).Returns(() => _accountsState);
        _accounts.SetupGet(a => a.Snapshot).Returns(() => new UserAccountListDto(1, null, [new UserAccountDto(Kid, "kid1", null, false, _controlled)]));
        _groups.SetupGet(g => g.LoadState).Returns(() => _groupsState);
        _groups.SetupGet(g => g.Snapshot).Returns(() => _snapshot);
        _groups.Setup(g => g.CreateAsync(It.IsAny<string>())).ReturnsAsync(true);
        _groups.Setup(g => g.RenameAsync(It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(true);
        _groups.Setup(g => g.DeleteAsync(It.IsAny<long>())).ReturnsAsync(true);
        _groups.Setup(g => g.MoveAsync(It.IsAny<string>(), It.IsAny<long?>())).ReturnsAsync(true);
        _snapshot = Snapshot(1, [Group(7, "Games"), Group(8, "school")], App(PaintPath, "Paint", null), App(LauncherPath, "Launcher", 7), App(EditorPath, "Editor", null));
    }

    // ---------- States (AC-5) ----------

    [Theory]
    [InlineData("de-DE", AccountsLoadState.NotAvailable, true, AccountsLoadState.Ready, AppGroupsState.NoData, "Keine Daten verfügbar")]
    [InlineData("en-US", AccountsLoadState.Loading, true, AccountsLoadState.Ready, AppGroupsState.Loading, "Loading …")]
    [InlineData("en-US", AccountsLoadState.Ready, false, AccountsLoadState.Ready, AppGroupsState.NoControlledAccounts, "No accounts under parental control. Select accounts under Settings.")]
    [InlineData("de-DE", AccountsLoadState.Ready, true, AccountsLoadState.Loading, AppGroupsState.Loading, "Wird geladen …")]
    [InlineData("en-US", AccountsLoadState.Ready, true, AccountsLoadState.NotAvailable, AppGroupsState.NoData, "No data available")]
    [InlineData("en-US", AccountsLoadState.Ready, true, AccountsLoadState.Ready, AppGroupsState.Ready, "")]
    public void States_AndStatusText(string culture, AccountsLoadState accounts, bool controlled, AccountsLoadState groups, AppGroupsState expected, string text)
    {
        (_accountsState, _controlled, _groupsState) = (accounts, controlled, groups);

        var (state, status, hasStatus, sections) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.State, vm.StatusText, vm.HasStatus, vm.Sections.Count);
        });

        Assert.Equal((expected, text, expected != AppGroupsState.Ready, expected == AppGroupsState.Ready ? 3 : 0), (state, status, hasStatus, sections));
    }

    [Fact]
    public void AccountListMissing_NoControlledAccounts()
    {
        _accounts.SetupGet(a => a.Snapshot).Returns((UserAccountListDto?)null);

        Assert.Equal(AppGroupsState.NoControlledAccounts, Create().State);
    }

    [Fact]
    public async Task AddGroup_WithoutSnapshot_NameOnlyCheckedByTheRules()
    {
        _snapshot = null;
        _dialogs.Setup(d => d.PromptTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>()))
            .ReturnsAsync("Games");
        var vm = Create();

        await vm.AddGroupCommand.ExecuteAsync(null);

        _groups.Verify(g => g.CreateAsync("Games"), Times.Once);
    }

    [Fact]
    public void State_FollowsTheModels_WithPropertyChanged()
    {
        _groupsState = AccountsLoadState.Loading;
        var vm = Create();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _groupsState = AccountsLoadState.Ready;
        _groups.Raise(g => g.Changed += null);
        _accountsState = AccountsLoadState.NotAvailable;
        _accounts.Raise(a => a.Changed += null);

        Assert.Equal((AppGroupsState.NoData, 0), (vm.State, vm.Sections.Count));
        Assert.Contains(nameof(AppGroupsViewModel.IsReady), changed);
    }

    // ---------- Sections and apps (AC-2, AC-3, AC-4) ----------

    [Fact]
    public void Sections_GroupsByNameIgnoringCase_UngroupedLast()
    {
        _snapshot = Snapshot(1, [Group(8, "school"), Group(7, "Games"), Group(9, "Ärger"), Group(6, "games")]);

        var vm = TestSupport.InCulture("de-DE", Create);

        Assert.Equal([(9L, "Ärger"), (6L, "games"), (7L, "Games"), (8L, "school"), (null, "In keiner Gruppe")], vm.Sections.Select(s => (s.GroupId, s.Title)));
        Assert.Equal([true, true, true, true, false], vm.Sections.Select(s => s.IsGroup));
    }

    [Fact]
    public void Apps_InTheirSection_SortedByNameThenPath_UnknownGroupUngrouped()
    {
        _snapshot = Snapshot(1, [Group(7, "Games")], App(PaintPath, "paint", null), App(@"C:\b.exe", "Editor", null), App(@"C:\a.exe", "editor", null), App(LauncherPath, "Launcher", 99));

        var vm = Create();

        Assert.Equal([@"C:\a.exe", @"C:\b.exe", LauncherPath, PaintPath], vm.Sections[1].Apps.Select(a => a.ProgramPath));
        Assert.Same(vm.Sections[1], vm.Sections[1].Apps[0].Section);
    }

    [Theory]
    [InlineData("de-DE", "Keine Apps in dieser Gruppe. Apps hierher ziehen.", "Alle Apps sind in einer Gruppe.")]
    [InlineData("en-US", "No apps in this group. Drag apps here.", "All apps are in a group.")]
    public void EmptySections_Texts(string culture, string emptyGroup, string allGrouped)
    {
        _snapshot = Snapshot(1, [Group(7, "Games"), Group(8, "School")], App(PaintPath, "Paint", 7));

        var texts = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.Sections[0].ShowEmpty, vm.Sections[1].ShowEmpty, vm.Sections[1].EmptyText, vm.Sections[2].ShowEmpty, vm.Sections[2].EmptyText);
        });

        Assert.Equal((false, true, emptyGroup, true, allGrouped), texts);
    }

    [Fact]
    public void Snapshot_MergedInPlace_NewAppInItsPlace_RenamedGroupMoved()
    {
        var vm = Create();
        var games = vm.Sections[0];
        var paint = vm.Sections[2].Apps[1];
        var changes = new List<string?>();
        games.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        _snapshot = Snapshot(2, [Group(7, "Zoo"), Group(8, "school")], App(PaintPath, "MS Paint", null), App(LauncherPath, "Launcher", 7), App(EditorPath, "Editor", null), App(@"C:\f.exe", "Fortnite", null));
        _groups.Raise(g => g.Changed += null);

        Assert.Equal([(8L, "school"), (7L, "Zoo")], vm.Sections.Take(2).Select(s => (s.GroupId!.Value, s.Title)));
        Assert.Same(games, vm.Sections[1]);
        Assert.Same(paint, vm.Sections[2].Apps[2]);
        Assert.Equal(["Editor", "Fortnite", "MS Paint"], vm.Sections[2].Apps.Select(a => a.DisplayName));
        Assert.Contains(nameof(AppGroupSectionViewModel.Title), changes);
    }

    // ---------- Create, rename, delete (AC-7 to AC-10) ----------

    [Fact]
    public async Task AddGroup_InvalidNamesPromptAgainWithMessageAndText_ValidNameCreatedTrimmed()
    {
        _dialogs.SetupSequence(d => d.PromptTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), 160))
            .ReturnsAsync("games")
            .ReturnsAsync("  ")
            .ReturnsAsync("  Music ");
        var vm = Create();

        await TestSupport.InCulture("en-US", () => vm.AddGroupCommand.ExecuteAsync(null));

        _dialogs.Verify(d => d.PromptTextAsync("New app group", "Name", "OK", "Cancel", null, 160), Times.Once);
        _dialogs.Verify(d => d.PromptTextAsync("New app group", "A group with this name already exists.\n\nName", "OK", "Cancel", "games", 160), Times.Once);
        _dialogs.Verify(d => d.PromptTextAsync("New app group", "Enter a name with 1 to 40 characters.\n\nName", "OK", "Cancel", "  ", 160), Times.Once);
        _groups.Verify(g => g.CreateAsync("Music"), Times.Once);
    }

    [Fact]
    public async Task AddGroup_Cancelled_NothingCreated()
    {
        _dialogs.Setup(d => d.PromptTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>()))
            .ReturnsAsync((string?)null);
        var vm = Create();

        await vm.AddGroupCommand.ExecuteAsync(null);

        _groups.Verify(g => g.CreateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Rename_FilledWithTheName_UnchangedNameValid()
    {
        _dialogs.Setup(d => d.PromptTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>()))
            .ReturnsAsync((string _, string _, string _, string _, string? initial, int _) => initial);
        var vm = Create();

        await TestSupport.InCulture("de-DE", () => vm.Sections[0].RenameCommand.ExecuteAsync(null));

        _dialogs.Verify(d => d.PromptTextAsync("App-Gruppe umbenennen", "Name", "OK", "Abbrechen", "Games", 160), Times.Once);
        _groups.Verify(g => g.RenameAsync(7, "Games"), Times.Once);
    }

    [Fact]
    public async Task Rename_Cancelled_OrUngroupedSection_NothingSent()
    {
        var vm = Create();

        await vm.Sections[0].RenameCommand.ExecuteAsync(null);
        await vm.Sections[2].RenameCommand.ExecuteAsync(null);

        _groups.Verify(g => g.RenameAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.PromptTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Once);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Delete_AskedFirst(bool accepted, int deletes)
    {
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(accepted);
        var vm = Create();

        await TestSupport.InCulture("de-DE", () => vm.Sections[0].DeleteCommand.ExecuteAsync(null));

        _dialogs.Verify(d => d.ConfirmAsync(
            "App-Gruppe löschen",
            "Gruppe „Games“ löschen? Ihre Apps kommen nach „In keiner Gruppe“ und verwenden wieder ihre eigenen Applikations-Regeln.",
            "Löschen",
            "Abbrechen"));
        _groups.Verify(g => g.DeleteAsync(7), Times.Exactly(deletes));
    }

    [Fact]
    public async Task Delete_UngroupedSection_NothingAsked()
    {
        var vm = Create();

        await vm.Sections[2].DeleteCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------- Moving (AC-11 to AC-14) ----------

    [Fact]
    public async Task Move_OtherSection_Sent_OwnSection_Nothing()
    {
        var vm = Create();
        var launcher = vm.Sections[0].Apps[0];

        await vm.MoveAsync(launcher, vm.Sections[0]);
        await vm.MoveAsync(launcher, vm.Sections[2]);
        await vm.MoveAsync(vm.Sections[2].Apps[0], vm.Sections[1]);

        _groups.Verify(g => g.MoveAsync(LauncherPath, null), Times.Once);
        _groups.Verify(g => g.MoveAsync(EditorPath, 8), Times.Once);
        _groups.Verify(g => g.MoveAsync(It.IsAny<string>(), It.IsAny<long?>()), Times.Exactly(2));
    }

    [Fact]
    public void MoveTargets_EverySectionButTheOwn()
    {
        var vm = Create();

        Assert.Equal([8L, null], vm.MoveTargets(vm.Sections[0].Apps[0]).Select(s => s.GroupId));
        Assert.Equal([7L, 8L], vm.MoveTargets(vm.Sections[2].Apps[0]).Select(s => s.GroupId));
    }

    [Fact]
    public async Task MoveCommand_ChosenTargetMoved_CancelNothing()
    {
        _dialogs.SetupSequence(d => d.ChooseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(1)
            .ReturnsAsync((int?)null);
        var vm = Create();
        var launcher = vm.Sections[0].Apps[0];

        await TestSupport.InCulture("de-DE", () => launcher.MoveCommand.ExecuteAsync(null));
        await launcher.MoveCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.ChooseAsync("Verschieben nach …", "Abbrechen", It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "school", "In keiner Gruppe" }))));
        _groups.Verify(g => g.MoveAsync(LauncherPath, null), Times.Once);
    }

    [Fact]
    public async Task DragAndDrop_HighlightFollowsThePointer_DropMovesAndClears()
    {
        var vm = Create();
        var paint = vm.Sections[2].Apps[1];
        vm.BeginDrag(paint);

        vm.DragOver(vm.Sections[0]);
        var first = string.Concat(vm.Sections.Select(s => s.IsDropTarget ? 'x' : '-'));
        vm.DragOver(vm.Sections[1]);
        var second = string.Concat(vm.Sections.Select(s => s.IsDropTarget ? 'x' : '-'));
        vm.DragLeave(vm.Sections[1]);
        var left = vm.Sections.Any(s => s.IsDropTarget);
        vm.DragOver(vm.Sections[1]);
        await vm.DropAsync(vm.Sections[1]);

        Assert.Equal(("x--", "-x-", false), (first, second, left));
        Assert.Equal((null, false), (vm.DraggedApp, vm.Sections.Any(s => s.IsDropTarget)));
        _groups.Verify(g => g.MoveAsync(PaintPath, 8), Times.Once);
    }

    [Fact]
    public async Task DragAndDrop_EndedOutside_NothingMovedAndCleared()
    {
        var vm = Create();
        vm.BeginDrag(vm.Sections[2].Apps[1]);
        vm.DragOver(vm.Sections[0]);

        vm.EndDrag();
        await vm.DropAsync(vm.Sections[0]);

        Assert.Equal((null, false), (vm.DraggedApp, vm.Sections.Any(s => s.IsDropTarget)));
        _groups.Verify(g => g.MoveAsync(It.IsAny<string>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task WriteFailed_MessageShown_ClearedByTheNextSuccess()
    {
        _groups.SetupSequence(g => g.MoveAsync(It.IsAny<string>(), It.IsAny<long?>())).ReturnsAsync(false).ReturnsAsync(true);
        var vm = Create();
        var launcher = vm.Sections[0].Apps[0];

        await TestSupport.InCulture("en-US", () => vm.MoveAsync(launcher, vm.Sections[2]));
        var failed = (vm.ErrorText, vm.HasError);
        await vm.MoveAsync(launcher, vm.Sections[2]);

        Assert.Equal((("The change could not be saved. Please try again.", true), false), (failed, vm.HasError));
    }

    [Fact]
    public void Guards_Throw()
    {
        var dispatcher = new ImmediateDispatcher();
        Assert.Throws<ArgumentNullException>(() => new AppGroupsViewModel(null!, _groups.Object, _dialogs.Object, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new AppGroupsViewModel(_accounts.Object, null!, _dialogs.Object, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new AppGroupsViewModel(_accounts.Object, _groups.Object, null!, dispatcher));
        Assert.Throws<ArgumentNullException>(() => new AppGroupsViewModel(_accounts.Object, _groups.Object, _dialogs.Object, null!));
        var vm = Create();
        Assert.Throws<ArgumentNullException>(() => vm.MoveTargets(null!));
        Assert.Throws<ArgumentNullException>(() => { _ = vm.MoveAsync(null!, vm.Sections[0]); });
        Assert.Throws<ArgumentNullException>(() => { _ = vm.MoveAsync(vm.Sections[0].Apps[0], null!); });
        Assert.Throws<ArgumentNullException>(() => vm.DragLeave(null!));
    }

    private AppGroupsViewModel Create() => new(_accounts.Object, _groups.Object, _dialogs.Object, new ImmediateDispatcher());

    private static AppGroupDto Group(long id, string name) => new(id, name);

    private static GroupableAppDto App(string path, string name, long? groupId) => new(path, name, groupId);

    private static AppGroupsDto Snapshot(long revision, AppGroupDto[] groups, params GroupableAppDto[] apps) => new(revision, null, groups, apps);
}
