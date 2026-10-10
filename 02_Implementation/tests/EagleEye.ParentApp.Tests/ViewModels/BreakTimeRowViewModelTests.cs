using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.Shared.Models;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class BreakTimeRowViewModelTests
{
    private static readonly BreakTimeEntryDto Entry = new(3, false, 1200, 1439, BreakTimeDays.Monday | BreakTimeDays.Friday);

    private readonly FakeHost _host = new();
    private readonly BreakTimeRowViewModel _row;

    public BreakTimeRowViewModelTests()
    {
        _row = new BreakTimeRowViewModel(Entry, _host);
    }

    [Fact]
    public void New_ShowsTheConfirmedValues()
    {
        Assert.Equal(
            (3L, false, "20:00", "23:59", true, false, false, false, true, false, false),
            (_row.EntryId, _row.IsActive, _row.StartText, _row.EndText, _row.Monday, _row.Tuesday, _row.Wednesday, _row.Thursday, _row.Friday, _row.Saturday, _row.Sunday));
    }

    [Fact]
    public async Task IsActive_Click_SentAtOnce()
    {
        _row.IsActive = true;
        await _row.LastWrite;

        _host.Model.Verify(m => m.SetActiveAsync(3, true), Times.Once);
    }

    [Fact]
    public void IsActive_SameValue_NotSent()
    {
        _row.IsActive = false;

        _host.Model.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsActive_PendingKeepsTheRequestedValue_RejectedReturnsToTheConfirmed()
    {
        var result = new TaskCompletionSource<bool>();
        _host.Model.Setup(m => m.SetActiveAsync(3, true)).Returns(result.Task);
        _row.IsActive = true;

        _row.Update(Entry);
        var whilePending = _row.IsActive;
        result.SetResult(false);
        await _row.LastWrite;

        Assert.Equal((true, false), (whilePending, _row.IsActive));
    }

    [Fact]
    public async Task IsActive_ConfirmedByASnapshot_ShowsTheConfirmedValue()
    {
        _row.IsActive = true;
        await _row.LastWrite;

        _row.Update(Entry with { IsActive = true });

        Assert.True(_row.IsActive);
    }

    [Fact]
    public async Task Days_TickAndUntick_Sent()
    {
        _row.Sunday = true;
        await _row.LastWrite;
        _row.Monday = false;
        await _row.LastWrite;

        _host.Model.Verify(m => m.SetDayAsync(3, DayOfWeek.Sunday, true), Times.Once);
        _host.Model.Verify(m => m.SetDayAsync(3, DayOfWeek.Monday, false), Times.Once);
    }

    [Fact]
    public void Days_EverySetter_Sends()
    {
        _row.Tuesday = true;
        _row.Wednesday = true;
        _row.Thursday = true;
        _row.Saturday = true;
        _row.Friday = false;

        Assert.Equal(5, _host.Model.Invocations.Count);
    }

    [Fact]
    public void Days_SameValue_NotSent()
    {
        _row.Monday = true;

        _host.Model.VerifyNoOtherCalls();
    }

    [Fact]
    public void Days_UntickTheLastDay_RefusedStaysTickedWithMessage()
    {
        var row = new BreakTimeRowViewModel(Entry with { Days = BreakTimeDays.Sunday }, _host);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Sunday = false;

        Assert.Equal((true, EditProblem.NoDaySelected), (row.Sunday, _host.Problems.Single()));
        Assert.Equal([nameof(BreakTimeRowViewModel.Sunday)], changed);
        _host.Model.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("1800", "18:00", 1080)]
    [InlineData("7:30", "07:30", 450)]
    public void CommitStart_Valid_NormalizedAndSentWhilePending(string typed, string shown, int minute)
    {
        _host.Model.Setup(m => m.SetTimeAsync(3, BreakTimeBoundary.Start, minute)).Returns(new TaskCompletionSource<bool>().Task);
        _row.StartText = typed;
        _row.CommitStart();
        var text = _row.StartText;

        Assert.Equal(shown, text);
        _host.Model.Verify(m => m.SetTimeAsync(3, BreakTimeBoundary.Start, minute), Times.Once);
    }

    [Fact]
    public async Task CommitEnd_Valid_Sent()
    {
        _row.EndText = "21";
        _row.CommitEnd();
        await _row.LastWrite;

        _host.Model.Verify(m => m.SetTimeAsync(3, BreakTimeBoundary.End, 1260), Times.Once);
    }

    [Theory]
    [InlineData("24:00", EditProblem.InvalidTime)]
    [InlineData("abc", EditProblem.InvalidTime)]
    [InlineData("", EditProblem.InvalidTime)]
    [InlineData("19:00", EditProblem.EndNotAfterStart)]
    public void CommitEnd_Refused_ReturnsToTheConfirmedValueWithMessage(string typed, EditProblem problem)
    {
        _row.EndText = typed;

        _row.CommitEnd();

        Assert.Equal(("23:59", problem, false), (_row.EndText, _host.Problems.Single(), _row.HasUncommittedTime));
        _host.Model.VerifyNoOtherCalls();
    }

    [Fact]
    public void CommitStart_SameTimeTypedDifferently_NormalizedNotSent()
    {
        _row.StartText = "2000";

        _row.CommitStart();

        Assert.Equal("20:00", _row.StartText);
        _host.Model.VerifyNoOtherCalls();
    }

    [Fact]
    public void Commit_NothingTyped_NothingHappens()
    {
        _row.CommitStart();
        _row.CommitEnd();

        Assert.Empty(_host.Problems);
        _host.Model.VerifyNoOtherCalls();
    }

    [Fact]
    public void Update_FieldWithTyping_NotOverwrittenOthersUpdated()
    {
        _row.StartText = "18";

        _row.Update(Entry with { StartMinute = 1140, EndMinute = 1380, IsActive = true });

        Assert.Equal(("18", "23:00", true, true), (_row.StartText, _row.EndText, _row.IsActive, _row.HasUncommittedTime));
    }

    [Fact]
    public void Update_EndBeingTyped_StartUpdated()
    {
        _row.EndText = "22";

        _row.Update(Entry with { StartMinute = 1140 });

        Assert.Equal(("19:00", "22"), (_row.StartText, _row.EndText));
    }

    [Fact]
    public void Update_DaysChangedElsewhere_Applied()
    {
        _row.Update(Entry with { Days = BreakTimeDays.Saturday | BreakTimeDays.Sunday });

        Assert.Equal((false, false, true, true), (_row.Monday, _row.Friday, _row.Saturday, _row.Sunday));
    }

    [Fact]
    public async Task TwoWritesOfOneField_StaysPendingUntilBothEnd()
    {
        var first = new TaskCompletionSource<bool>();
        _host.Model.Setup(m => m.SetActiveAsync(3, true)).Returns(first.Task);
        _row.IsActive = true;
        var firstWrite = _row.LastWrite;
        _row.IsActive = false;
        await _row.LastWrite;

        _row.Update(Entry with { IsActive = true });
        var whileFirstPending = _row.IsActive;
        first.SetResult(true);
        await firstWrite;

        Assert.Equal((false, true), (whileFirstPending, _row.IsActive));
    }

    [Fact]
    public async Task TimePending_SnapshotDoesNotOverwrite()
    {
        var result = new TaskCompletionSource<bool>();
        _host.Model.Setup(m => m.SetTimeAsync(3, BreakTimeBoundary.End, 1260)).Returns(result.Task);
        _row.EndText = "21:00";
        _row.CommitEnd();

        _row.Update(Entry);
        var whilePending = _row.EndText;
        _row.Update(Entry with { EndMinute = 1260 });
        result.SetResult(true);
        await _row.LastWrite;

        Assert.Equal(("21:00", "21:00"), (whilePending, _row.EndText));
    }

    [Fact]
    public async Task Delete_SentAtOnce()
    {
        _row.DeleteCommand.Execute(null);
        await _row.LastWrite;

        _host.Model.Verify(m => m.DeleteEntryAsync(3), Times.Once);
    }

    [Fact]
    public void Texts_SameValue_NotDirty()
    {
        _row.StartText = "20:00";
        _row.EndText = "23:59";

        Assert.False(_row.HasUncommittedTime);
    }

    [Fact]
    public void Update_DayPending_NotOverwritten()
    {
        _host.Model.Setup(m => m.SetDayAsync(3, DayOfWeek.Sunday, true)).Returns(new TaskCompletionSource<bool>().Task);
        _row.Sunday = true;

        _row.Update(Entry);

        Assert.True(_row.Sunday);
    }

    [Fact]
    public void Texts_NullSetToEmpty()
    {
        _row.StartText = null!;
        _row.EndText = null!;

        Assert.Equal((string.Empty, string.Empty), (_row.StartText, _row.EndText));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new BreakTimeRowViewModel(null!, _host));
        Assert.Throws<ArgumentNullException>(() => new BreakTimeRowViewModel(Entry, null!));
        Assert.Throws<ArgumentNullException>(() => _row.Update(null!));
    }

    private sealed class FakeHost : IBreakTimeRowHost
    {
        public Mock<IAccountRulesModel> Model { get; } = new();

        public List<EditProblem> Problems { get; } = [];

        public void Post(Action action) => action();

        public Task<bool> SendAsync(Func<IAccountRulesModel, Task<bool>> write) => write(Model.Object);

        public void ShowProblem(EditProblem problem) => Problems.Add(problem);
    }
}
