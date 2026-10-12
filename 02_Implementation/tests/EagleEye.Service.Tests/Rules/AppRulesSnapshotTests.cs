using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class AppRulesSnapshotTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";

    [Fact]
    public void From_GroupsByAccount_LeavesIgnoredOut()
    {
        var stored = new StoredAppRules(
            [
                new StoredAppRecord(Kid, 1, @"C:\a.exe", "A", 30, BreakTimeDays.Monday),
                new StoredAppRecord(Kid.ToLowerInvariant(), 2, @"C:\b.exe", "B", 0, BreakTimeDays.None),
                new StoredAppRecord(Kid2, 3, @"C:\Windows\explorer.exe", "Explorer", 0, BreakTimeDays.None),
            ],
            [new StoredDisplayText(Kid2, "Ende")]);

        var snapshot = AppRulesSnapshot.From(stored, path => path.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Equal([1L, 2L], snapshot.For(Kid).Apps.Keys.Order());
        Assert.Empty(snapshot.For(Kid2).Apps);
        Assert.Equal("Ende", snapshot.For(Kid2).BudgetText);
        Assert.Equal(2, snapshot.AccountSids.Count());
    }

    [Fact]
    public void RuleFor_CaseInsensitivePath_NullWhenUnknown()
    {
        var snapshot = AppRulesSnapshot.Empty.With(Kid, AccountAppRules.Empty.WithApp(new AppRuleEntry(1, @"C:\A.exe", "A", 5, BreakTimeDays.All)));

        Assert.Equal(1, snapshot.RuleFor(Kid.ToLowerInvariant(), @"c:\a.EXE")!.AppId);
        Assert.Null(snapshot.RuleFor(Kid, @"C:\b.exe"));
        Assert.Null(snapshot.RuleFor(Kid2, @"C:\A.exe"));
    }

    [Fact]
    public void WithApp_PathChanged_OldPathRemoved()
    {
        var rules = AccountAppRules.Empty.WithApp(new AppRuleEntry(1, @"C:\old.exe", "A", 5, BreakTimeDays.All));

        rules = rules.WithApp(new AppRuleEntry(1, @"C:\new.exe", "A", 5, BreakTimeDays.All));

        Assert.Equal([@"C:\new.exe"], rules.ByPath.Keys);
    }

    [Fact]
    public void With_EmptyRules_RemovesTheAccount()
    {
        var snapshot = AppRulesSnapshot.Empty.With(Kid, AccountAppRules.Empty.WithBudgetText("x"));

        Assert.Single(snapshot.AccountSids);
        Assert.Empty(snapshot.With(Kid, AccountAppRules.Empty.WithBudgetText(null)).AccountSids);
    }

    [Fact]
    public void Without_RemovesAccounts()
    {
        var snapshot = AppRulesSnapshot.Empty.With(Kid, AccountAppRules.Empty.WithBudgetText("x")).With(Kid2, AccountAppRules.Empty.WithBudgetText("y"));

        Assert.Equal([Kid2], snapshot.Without([Kid]).AccountSids);
    }

    [Theory]
    [InlineData(30, BreakTimeDays.Monday, DayOfWeek.Monday, true)]
    [InlineData(30, BreakTimeDays.Monday, DayOfWeek.Tuesday, false)]
    [InlineData(0, BreakTimeDays.All, DayOfWeek.Monday, false)]
    public void AppRuleEntry_IsAllowedOn(int budget, BreakTimeDays days, DayOfWeek day, bool expected)
    {
        Assert.Equal(expected, new AppRuleEntry(1, "p", "A", budget, days).IsAllowedOn(day));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.From(null!, _ => false));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.From(new StoredAppRules([], []), null!));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.Empty.For(null!));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.Empty.RuleFor(Kid, null!));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.Empty.With(null!, AccountAppRules.Empty));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.Empty.With(Kid, null!));
        Assert.Throws<ArgumentNullException>(() => AppRulesSnapshot.Empty.Without(null!));
        Assert.Throws<ArgumentNullException>(() => AccountAppRules.Empty.WithApp(null!));
    }
}
