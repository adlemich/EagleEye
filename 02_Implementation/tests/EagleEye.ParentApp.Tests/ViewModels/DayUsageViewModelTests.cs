using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class DayUsageViewModelTests
{
    private static readonly DateOnly Day = new(2026, 10, 7);

    [Theory]
    [InlineData("de-DE", true, "Heute, 07.10.2026")]
    [InlineData("de-DE", false, "07.10.2026")]
    [InlineData("en-US", true, "Today, 10/7/2026")]
    [InlineData("en-US", false, "10/7/2026")]
    public void Heading_TodayAndOtherDays(string culture, bool isToday, string expected)
    {
        Assert.Equal(expected, TestSupport.InCulture(culture, () => new DayUsageViewModel(Day, isToday).Heading));
    }

    [Fact]
    public void Update_RowsSortedBySecondsThenName()
    {
        var day = new DayUsageViewModel(Day, false);

        TestSupport.InCulture("de-DE", () =>
        {
            day.Update([App(1, "Zoe", 60), App(2, "edge", 600), App(3, "Ärger", 60), App(4, "Anna", 60)], false);
            return 0;
        });

        Assert.Equal(["edge", "Anna", "Ärger", "Zoe"], day.Rows.Select(r => r.DisplayName));
    }

    [Fact]
    public void Update_MergesKeepsInstancesMovesAndRemoves()
    {
        var day = new DayUsageViewModel(Day, true);
        day.Update([App(1, "Notepad", 60), App(2, "Paint", 30), App(3, "Edge", 10)], true);
        var notepad = day.Rows[0];

        day.Update([App(1, "Notepad", 61), App(3, "Edge", 120)], true);

        Assert.Equal(["Edge", "Notepad"], day.Rows.Select(r => r.DisplayName));
        Assert.Same(notepad, day.Rows[1]);
        Assert.Equal("00:01", notepad.UsageText);
    }

    [Fact]
    public void TodayWithoutRows_ShowsNoUsageText()
    {
        var day = new DayUsageViewModel(Day, true);
        day.Update([], true);

        Assert.Equal((true, false), (day.ShowNoUsageToday, day.HasRows));
    }

    [Fact]
    public void PastDayWithoutRows_NoText()
    {
        var day = new DayUsageViewModel(Day, false);
        day.Update([], false);

        Assert.False(day.ShowNoUsageToday);
    }

    [Fact]
    public void Update_RowsAppearAndDisappear_HasRowsNotified()
    {
        var day = new DayUsageViewModel(Day, true);
        var changes = new List<string?>();
        day.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        day.Update([App(1, "Notepad", 1)], true);
        day.Update([App(1, "Notepad", 2)], true);
        day.Update([], true);

        Assert.Equal(2, changes.Count(c => c == nameof(DayUsageViewModel.HasRows)));
    }

    [Fact]
    public void Update_TodayBecomesPastDay_HeadingNotified()
    {
        var day = new DayUsageViewModel(Day, true);
        var changes = new List<string?>();
        day.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        day.Update([], false);

        Assert.Contains(nameof(DayUsageViewModel.Heading), changes);
        Assert.False(day.IsToday);
    }

    [Fact]
    public void Update_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DayUsageViewModel(Day, true).Update(null!, true));
    }

    private static AppUsageDto App(long id, string name, long seconds) => new(id, name, seconds);
}
