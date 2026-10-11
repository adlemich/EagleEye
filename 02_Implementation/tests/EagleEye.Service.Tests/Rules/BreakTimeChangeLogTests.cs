using EagleEye.Service.Rules;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class BreakTimeChangeLogTests
{
    private static readonly BreakTimeEntryDto Entry = new(3, true, 1200, 1439, BreakTimeDays.Monday | BreakTimeDays.Tuesday | BreakTimeDays.Wednesday | BreakTimeDays.Thursday | BreakTimeDays.Friday);

    [Fact]
    public void Added_DescribesTheNewEntry()
    {
        Assert.Equal("entry 4 added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su)", BreakTimeChangeLog.Added(new BreakTimeEntryDto(4, false, 1200, 1439, BreakTimeDays.All)));
    }

    [Fact]
    public void Changed_DescribesAllValues()
    {
        Assert.Equal("entry 3 changed: on, 20:00–23:59, Mo Tu We Th Fr", BreakTimeChangeLog.Changed(Entry));
    }

    [Fact]
    public void Deleted_NamesTheEntry()
    {
        Assert.Equal("entry 3 deleted", BreakTimeChangeLog.Deleted(3));
    }

    [Fact]
    public void Texts_ChangedAndReset()
    {
        Assert.Equal(("display text changed", "display text reset to the default"), (BreakTimeChangeLog.TextChanged(), BreakTimeChangeLog.TextReset()));
    }

    [Theory]
    [InlineData(BreakTimeDays.Saturday | BreakTimeDays.Sunday, "Sa Su")]
    [InlineData(BreakTimeDays.Sunday | BreakTimeDays.Monday, "Mo Su")]
    [InlineData(BreakTimeDays.None, "")]
    public void Days_MondayFirst(BreakTimeDays days, string expected)
    {
        Assert.Equal(expected, BreakTimeChangeLog.Days(days));
    }

    [Fact]
    public void Range_FormatsHhMm()
    {
        Assert.Equal("00:00–09:00", BreakTimeChangeLog.Range(new BreakTimeEntryDto(1, true, 0, 540, BreakTimeDays.All)));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "Mo")]
    [InlineData(DayOfWeek.Sunday, "Su")]
    public void Weekday_Abbreviation(DayOfWeek day, string expected)
    {
        Assert.Equal(expected, BreakTimeChangeLog.Weekday(day));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BreakTimeChangeLog.Added(null!));
        Assert.Throws<ArgumentNullException>(() => BreakTimeChangeLog.Range(null!));
    }
}
