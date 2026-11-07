using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Settings;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class WarningSettingsViewModelTests
{
    private readonly Mock<IWarningSettingsModel> _model = new();
    private AccountsLoadState _state = AccountsLoadState.Ready;
    private WarningSettingsDto? _snapshot = new(2, null, 5, 1);

    public WarningSettingsViewModelTests()
    {
        _model.SetupGet(m => m.LoadState).Returns(() => _state);
        _model.SetupGet(m => m.Snapshot).Returns(() => _snapshot);
        _model.Setup(m => m.SetMinutesAsync(It.IsAny<WarningLevel>(), It.IsAny<int>())).ReturnsAsync(true);

    }

    [Theory]
    [InlineData("de-DE", "Frühe Warnung", "Finale Warnung")]
    [InlineData("en-US", "Early warning", "Final warning")]
    public void TwoFixedRows_LabelsAndMinutes(string culture, string early, string final)
    {
        var result = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.Early!.Label, vm.Final!.Label, vm.Early.MinutesText, vm.Final.MinutesText);
        });

        Assert.Equal((early, final, "5", "1"), result);
    }

    [Fact]
    public void Row_HasNoTextMembers()
    {
        var names = typeof(WarningRowViewModel).GetMembers().Select(m => m.Name).ToHashSet();

        Assert.DoesNotContain("Text", names);
        Assert.DoesNotContain("TextMaxLength", names);
        Assert.DoesNotContain("CommitText", names);
        Assert.Null(typeof(IWarningSettingsModel).GetMethod("SetTextAsync"));
    }

    [Theory]
    [InlineData("de-DE", AccountsLoadState.NotAvailable, "Keine Daten verfügbar")]
    [InlineData("en-US", AccountsLoadState.Loading, "Loading …")]
    [InlineData("en-US", AccountsLoadState.Ready, "")]
    public void States(string culture, AccountsLoadState state, string text)
    {
        _state = state;

        var (status, hasStatus, ready, rows) = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.StatusText, vm.HasStatus, vm.IsReady, vm.Early is not null);
        });

        Assert.Equal((text, state != AccountsLoadState.Ready, state == AccountsLoadState.Ready, state == AccountsLoadState.Ready), (status, hasStatus, ready, rows));
    }

    [Fact]
    public async Task Minutes_Valid_SentAndNormalized()
    {
        _model.Setup(m => m.SetMinutesAsync(WarningLevel.Early, 3)).Callback(() => _snapshot = _snapshot! with { Revision = 3, EarlyMinutes = 3 }).ReturnsAsync(true);
        var vm = Create();
        vm.Early!.MinutesText = "03";

        vm.Early.CommitMinutes();
        await vm.Early.LastWrite;

        _model.Verify(m => m.SetMinutesAsync(WarningLevel.Early, 3), Times.Once);
        Assert.Equal("3", vm.Early.MinutesText);
    }

    [Theory]
    [InlineData(WarningLevel.Early, "0", "Enter minutes between 1 and 59.")]
    [InlineData(WarningLevel.Early, "60", "Enter minutes between 1 and 59.")]
    [InlineData(WarningLevel.Early, "abc", "Enter minutes between 1 and 59.")]
    [InlineData(WarningLevel.Early, "2.5", "Enter minutes between 1 and 59.")]
    [InlineData(WarningLevel.Early, "", "Enter minutes between 1 and 59.")]
    [InlineData(WarningLevel.Early, "1", "The final warning must be less minutes before the end than the early warning.")]
    [InlineData(WarningLevel.Final, "5", "The final warning must be less minutes before the end than the early warning.")]
    [InlineData(WarningLevel.Final, "6", "The final warning must be less minutes before the end than the early warning.")]
    public void Minutes_Invalid_RevertedWithMessage(WarningLevel level, string text, string message)
    {
        var vm = TestSupport.InCulture("en-US", Create);
        var row = level == WarningLevel.Early ? vm.Early! : vm.Final!;

        TestSupport.InCulture("en-US", () =>
        {
            row.MinutesText = text;
            row.CommitMinutes();
            return true;
        });

        Assert.Equal(message, vm.ErrorText);
        Assert.Equal(level == WarningLevel.Early ? "5" : "1", row.MinutesText);
        _model.Verify(m => m.SetMinutesAsync(It.IsAny<WarningLevel>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Minutes_UnchangedOrNotDirty_NotSent()
    {
        var vm = Create();
        vm.Final!.CommitMinutes();
        vm.Final.MinutesText = "01";

        vm.Final.CommitMinutes();

        _model.Verify(m => m.SetMinutesAsync(It.IsAny<WarningLevel>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Minutes_PendingNotOverwritten_ThenConfirmedShown()
    {
        var pending = new TaskCompletionSource<bool>();
        _model.Setup(m => m.SetMinutesAsync(WarningLevel.Final, 2)).Returns(pending.Task);
        var vm = Create();
        vm.Final!.MinutesText = "2";
        vm.Final.CommitMinutes();

        _snapshot = _snapshot! with { Revision = 3 };
        _model.Raise(m => m.Changed += null);
        Assert.Equal("2", vm.Final.MinutesText);

        pending.SetResult(false);
        Assert.Equal("1", vm.Final.MinutesText);
    }

    [Fact]
    public void WriteFails_Message_NextSuccessClears()
    {
        _model.Setup(m => m.SetMinutesAsync(WarningLevel.Early, 7)).ReturnsAsync(false);
        var vm = TestSupport.InCulture("de-DE", Create);

        TestSupport.InCulture("de-DE", () =>
        {
            vm.Early!.MinutesText = "7";
            vm.Early.CommitMinutes();
            return true;
        });
        Assert.Equal("Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.", vm.ErrorText);
        Assert.True(vm.HasError);

        vm.Early!.MinutesText = "8";
        vm.Early.CommitMinutes();
        Assert.False(vm.HasError);
    }

    [Fact]
    public void FlushPendingEdits_CommitsBothRows_AlsoBeforeTheFirstSnapshot()
    {
        _state = AccountsLoadState.Loading;
        _snapshot = null;
        var notReady = Create();
        notReady.FlushPendingEdits();

        _state = AccountsLoadState.Ready;
        _snapshot = new WarningSettingsDto(2, null, 5, 1);
        var vm = Create();
        vm.Early!.MinutesText = "7";
        vm.Final!.MinutesText = "2";

        vm.FlushPendingEdits();

        _model.Verify(m => m.SetMinutesAsync(WarningLevel.Early, 7), Times.Once);
        _model.Verify(m => m.SetMinutesAsync(WarningLevel.Final, 2), Times.Once);
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new WarningSettingsViewModel(null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new WarningSettingsViewModel(_model.Object, null!));
    }

    private WarningSettingsViewModel Create() => new(_model.Object, new ImmediateDispatcher());
}
