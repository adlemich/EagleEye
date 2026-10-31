using EagleEye.Service.Data;
using EagleEye.Service.Enforcement;
using EagleEye.Service.Groups;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class AppGroupsSnapshotTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\notepad.exe";
    private const string Paint = @"C:\Windows\mspaint.exe";

    [Fact]
    public void From_StoredRows()
    {
        var snapshot = AppGroupsSnapshot.From(new StoredAppGroups(
            [new StoredAppGroup(1, "Test")], [new StoredAppGroupMember(Notepad, 1)], [new StoredAppGroupRule(1, Kid, 60, BreakTimeDays.All)], []));

        Assert.Equal("Test", snapshot.Names[1]);
        Assert.Equal(1, snapshot.GroupOf(Notepad.ToUpperInvariant()));
        Assert.Null(snapshot.GroupOf(Paint));
        Assert.Equal([Notepad], snapshot.MembersOf(1));
        Assert.Equal(new LimitRule(60, BreakTimeDays.All), snapshot.RuleFor(1, Kid.ToLowerInvariant()));
        Assert.Equal(LimitRule.Default, snapshot.RuleFor(1, Kid2));
    }

    [Fact]
    public void WithMember_MovesAndRemoves()
    {
        var snapshot = AppGroupsSnapshot.Empty.WithGroup(1, "A").WithGroup(2, "B").WithMember(Notepad, 1);

        var moved = snapshot.WithMember(Notepad, 2);
        var removed = moved.WithMember(Notepad, null);

        Assert.Equal((1L, 2L, (long?)null), (snapshot.GroupOf(Notepad)!.Value, moved.GroupOf(Notepad)!.Value, removed.GroupOf(Notepad)));
    }

    [Fact]
    public void WithRule_DefaultRemovesTheEntry()
    {
        var snapshot = AppGroupsSnapshot.Empty.WithGroup(1, "A").WithRule(1, Kid, new LimitRule(30, BreakTimeDays.Monday));

        var reset = snapshot.WithRule(1, Kid, LimitRule.Default);

        Assert.Equal((1, 0), (snapshot.Rules.Count, reset.Rules.Count));
    }

    [Fact]
    public void WithoutGroup_RemovesNameMembersAndRules()
    {
        var snapshot = AppGroupsSnapshot.Empty.WithGroup(1, "A").WithGroup(2, "B")
            .WithMember(Notepad, 1).WithMember(Paint, 2).WithRule(1, Kid, new LimitRule(30, BreakTimeDays.All)).WithRule(2, Kid, new LimitRule(30, BreakTimeDays.All));

        var without = snapshot.WithoutGroup(1);

        Assert.Equal((1, 1, 1), (without.Names.Count, without.MembersByPath.Count, without.Rules.Count));
        Assert.Empty(without.MembersOf(1));
    }

    [Fact]
    public void WithoutRulesOf_KeepsGroupsAndOtherAccounts()
    {
        var snapshot = AppGroupsSnapshot.Empty.WithGroup(1, "A").WithMember(Notepad, 1)
            .WithRule(1, Kid, new LimitRule(30, BreakTimeDays.All)).WithRule(1, Kid2, new LimitRule(45, BreakTimeDays.All));

        var purged = snapshot.WithoutRulesOf([Kid.ToLowerInvariant()]);

        Assert.Equal((1, 1, 45), (purged.Names.Count, purged.MembersByPath.Count, purged.RuleFor(1, Kid2).BudgetMinutes));
        Assert.Equal(LimitRule.Default, purged.RuleFor(1, Kid));
    }

    [Fact]
    public void Guards()
    {
        Assert.Throws<ArgumentNullException>(() => AppGroupsSnapshot.From(null!));
        Assert.Throws<ArgumentNullException>(() => AppGroupsSnapshot.Empty.GroupOf(null!));
        Assert.Throws<ArgumentNullException>(() => AppGroupsSnapshot.Empty.RuleFor(1, null!));
    }

    [Fact]
    public void LimitRule_IsAllowedOn()
    {
        Assert.True(new LimitRule(60, BreakTimeDays.Saturday).IsAllowedOn(DayOfWeek.Saturday));
        Assert.False(new LimitRule(60, BreakTimeDays.Saturday).IsAllowedOn(DayOfWeek.Sunday));
        Assert.False(LimitRule.Default.IsAllowedOn(DayOfWeek.Saturday));
    }
}
