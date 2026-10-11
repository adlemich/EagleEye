using EagleEye.ParentApp.Core.Rules;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.Rules;

public sealed class BreakTimeEditRulesTests
{
    private static readonly BreakTimeEntryDto Entry = new(1, true, 1200, 1439, BreakTimeDays.Monday | BreakTimeDays.Friday);

    [Theory]
    [InlineData(BreakTimeBoundary.Start, "18:00", EditProblem.None, 1080)]
    [InlineData(BreakTimeBoundary.End, "21", EditProblem.None, 1260)]
    [InlineData(BreakTimeBoundary.End, "19:00", EditProblem.EndNotAfterStart, 1140)]
    [InlineData(BreakTimeBoundary.End, "20:00", EditProblem.EndNotAfterStart, 1200)]
    [InlineData(BreakTimeBoundary.Start, "23:59", EditProblem.EndNotAfterStart, 1439)]
    [InlineData(BreakTimeBoundary.Start, "24:00", EditProblem.InvalidTime, 0)]
    [InlineData(BreakTimeBoundary.End, "", EditProblem.InvalidTime, 0)]
    public void CheckTime_AgainstTheOtherConfirmedBoundary(BreakTimeBoundary boundary, string text, EditProblem expected, int expectedMinute)
    {
        Assert.Equal((expected, expectedMinute), (BreakTimeEditRules.CheckTime(Entry, boundary, text, out var minute), minute));
    }

    [Fact]
    public void CheckTime_EndBeforeStartMove_StartThenEndWorks()
    {
        // AC-10 note: 08:00–09:00 to 10:00–11:00: first the end, then the start.
        var morning = Entry with { StartMinute = 480, EndMinute = 540 };

        Assert.Equal(EditProblem.EndNotAfterStart, BreakTimeEditRules.CheckTime(morning, BreakTimeBoundary.Start, "10:00", out _));
        Assert.Equal(EditProblem.None, BreakTimeEditRules.CheckTime(morning, BreakTimeBoundary.End, "11:00", out _));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, false, EditProblem.None)]
    [InlineData(DayOfWeek.Tuesday, true, EditProblem.None)]
    [InlineData(DayOfWeek.Tuesday, false, EditProblem.None)]
    public void CheckDay_SomeDayStaysTicked(DayOfWeek day, bool isSelected, EditProblem expected)
    {
        Assert.Equal(expected, BreakTimeEditRules.CheckDay(Entry.Days, day, isSelected));
    }

    [Fact]
    public void CheckDay_LastDay_Refused()
    {
        Assert.Equal(EditProblem.NoDaySelected, BreakTimeEditRules.CheckDay(BreakTimeDays.Sunday, DayOfWeek.Sunday, false));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public void CanAdd_Below20(int count, bool expected)
    {
        Assert.Equal(expected, BreakTimeEditRules.CanAdd(count));
    }

    [Fact]
    public void CheckTime_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BreakTimeEditRules.CheckTime(null!, BreakTimeBoundary.Start, "1", out _));
    }
}
