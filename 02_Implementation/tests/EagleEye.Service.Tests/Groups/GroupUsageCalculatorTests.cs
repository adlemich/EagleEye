using EagleEye.Service.Data;
using EagleEye.Service.Groups;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class GroupUsageCalculatorTests
{
    private const string Notepad = @"C:\Windows\notepad.exe";
    private const string Paint = @"C:\Windows\mspaint.exe";
    private const string Calc = @"C:\Windows\calc.exe";
    private static readonly DateOnly Day = new(2026, 10, 31);
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForDay_UnionOfCurrentMembersPerGroup()
    {
        var groups = AppGroupsSnapshot.Empty.WithGroup(1, "Test").WithGroup(2, "Other")
            .WithMember(Notepad, 1).WithMember(Paint.ToUpperInvariant(), 1).WithMember(Calc, 2);

        var lines = GroupUsageCalculator.ForDay([Span(Notepad, 0, 12), Span(Paint, 2, 12), Span(Calc, 0, 1)], groups);

        Assert.Equal([new GroupUsageDto(1, "Test", 720), new GroupUsageDto(2, "Other", 60)], lines.OrderBy(l => l.GroupId));
    }

    [Fact]
    public void ForDay_NoMemberSpans_NoLine()
    {
        var groups = AppGroupsSnapshot.Empty.WithGroup(1, "Test").WithMember(Notepad, 1);

        Assert.Empty(GroupUsageCalculator.ForDay([Span(Paint, 0, 5)], groups));
    }

    [Fact]
    public void ForDay_DeletedGroup_NoLine()
    {
        var groups = AppGroupsSnapshot.Empty.WithGroup(1, "Test").WithMember(Notepad, 1).WithoutGroup(1);

        Assert.Empty(GroupUsageCalculator.ForDay([Span(Notepad, 0, 5)], groups));
    }

    [Fact]
    public void ForDay_LessThanASecond_ListedWithZero()
    {
        var groups = AppGroupsSnapshot.Empty.WithGroup(1, "Test").WithMember(Notepad, 1);

        Assert.Equal(0, GroupUsageCalculator.ForDay([new StoredSpan(Day, Notepad, T0, T0.AddMilliseconds(400))], groups).Single().Seconds);
    }

    [Fact]
    public void ForDay_Guards()
    {
        Assert.Throws<ArgumentNullException>(() => GroupUsageCalculator.ForDay(null!, AppGroupsSnapshot.Empty));
        Assert.Throws<ArgumentNullException>(() => GroupUsageCalculator.ForDay([], null!));
    }

    private static StoredSpan Span(string path, int fromMinute, int toMinute) => new(Day, path, T0.AddMinutes(fromMinute), T0.AddMinutes(toMinute));
}
