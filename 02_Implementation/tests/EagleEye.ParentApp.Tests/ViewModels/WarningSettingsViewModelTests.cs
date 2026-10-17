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
    private WarningSettingsDto? _snapshot = new(2, null, 5, null, 1, "Schnell!");

    public WarningSettingsViewModelTests()
    {
        _model.SetupGet(m => m.LoadState).Returns(() => _state);
        _model.SetupGet(m => m.Snapshot).Returns(() => _snapshot);
        _model.Setup(m => m.SetMinutesAsync(It.IsAny<WarningLevel>(), It.IsAny<int>())).ReturnsAsync(true);
        _model.Setup(m => m.SetTextAsync(It.IsAny<WarningLevel>(), It.IsAny<string>())).ReturnsAsync(true);
    }

    [Theory]
    [InlineData("de-DE", "Frühe Warnung", "Finale Warnung", "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt.")]
    [InlineData("en-US", "Early warning", "Final warning", "Attention, you only have 5 minutes left! Save your progress.")]
    public void TwoFixedRows_LabelsMinutesAndTexts(string culture, string early, string final, string earlyText)
    {
        var result = TestSupport.InCulture(culture, () =>
        {
            var vm = Create();
            return (vm.Early!.Label, vm.Final!.Label, vm.Early.MinutesText, vm.Final.MinutesText, vm.Early.Text, vm.Final.Text, vm.Early.TextMaxLength);
        });

        Assert.Equal((early, final, "5", "1", earlyText, "Schnell!", 500), result);
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
    public async Task Text_ChangedSent_CustomEmptiedSentEmpty_DefaultUnchangedNotSent()
    {
        var vm = Create();
        vm.Early!.Text = "Bald Schluss";
        vm.Early.CommitText();
        await vm.Early.LastWrite;
        vm.Final!.Text = string.Empty;
        vm.Final.CommitText();
        await vm.Final.LastWrite;

        _model.Verify(m => m.SetTextAsync(WarningLevel.Early, "Bald Schluss"), Times.Once);
        _model.Verify(m => m.SetTextAsync(WarningLevel.Final, string.Empty), Times.Once);
    }

    [Fact]
    public void Text_EmptyWhileDefault_NothingSentDefaultShownAgain_TypingKept()
    {
        var vm = TestSupport.InCulture("en-US", Create);
        TestSupport.InCulture("en-US", () =>
        {
            vm.Early!.Text = string.Empty;
            vm.Early.CommitText();
            vm.Early.CommitText();
            return true;
        });
        Assert.Equal("Attention, you only have 5 minutes left! Save your progress.", vm.Early!.Text);

        vm.Final!.Text = "getippt";
        _snapshot = _snapshot! with { Revision = 5, FinalText = "von B" };
        _model.Raise(m => m.Changed += null);

        Assert.Equal("getippt", vm.Final.Text);
        _model.Verify(m => m.SetTextAsync(It.IsAny<WarningLevel>(), It.IsAny<string>()), Times.Never);
        vm.Final.Text = null!;
        Assert.Equal(string.Empty, vm.Final.Text);
    }

    [Fact]
    public void WriteFails_Message_NextSuccessClears()
    {
        _model.Setup(m => m.SetTextAsync(WarningLevel.Early, "x")).ReturnsAsync(false);
        var vm = TestSupport.InCulture("de-DE", Create);

        TestSupport.InCulture("de-DE", () =>
        {
            vm.Early!.Text = "x";
            vm.Early.CommitText();
            return true;
        });
        Assert.Equal("Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.", vm.ErrorText);
        Assert.True(vm.HasError);

        vm.Early!.Text = "y";
        vm.Early.CommitText();
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
        _snapshot = new WarningSettingsDto(2, null, 5, null, 1, null);
        var vm = Create();
        vm.Early!.MinutesText = "7";
        vm.Final!.Text = "Jetzt!";

        vm.FlushPendingEdits();

        _model.Verify(m => m.SetMinutesAsync(WarningLevel.Early, 7), Times.Once);
        _model.Verify(m => m.SetTextAsync(WarningLevel.Final, "Jetzt!"), Times.Once);
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new WarningSettingsViewModel(null!, new ImmediateDispatcher()));
        Assert.Throws<ArgumentNullException>(() => new WarningSettingsViewModel(_model.Object, null!));
    }

    private WarningSettingsViewModel Create() => new(_model.Object, new ImmediateDispatcher());
}
