using EagleEye.Service.Rules;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class AppRuleChangeLogTests
{
    [Fact]
    public void RuleChanged_WithDays()
    {
        var app = new AppRuleEntry(1, "p", "Minecraft", 45, BreakTimeDays.Monday | BreakTimeDays.Tuesday | BreakTimeDays.Wednesday | BreakTimeDays.Thursday | BreakTimeDays.Friday);

        Assert.Equal("app rule changed: Minecraft, 0:45, Mo Tu We Th Fr", AppRuleChangeLog.RuleChanged(app));
    }

    [Fact]
    public void RuleChanged_NoDay()
    {
        Assert.Equal("app rule changed: Editor, 0:00, no day", AppRuleChangeLog.RuleChanged(new AppRuleEntry(1, "p", "Editor", 0, BreakTimeDays.None)));
    }

    [Fact]
    public void Texts()
    {
        Assert.Equal("budget text changed", AppRuleChangeLog.TextChanged());
        Assert.Equal("budget text reset to the default", AppRuleChangeLog.TextReset());
        Assert.Equal("warning levels changed: early 5 min, final 1 min", AppRuleChangeLog.LevelsChanged(5, 1));
        Assert.Equal("early warning text changed", AppRuleChangeLog.WarningTextChanged(WarningLevel.Early, false));
        Assert.Equal("final warning text reset to the default", AppRuleChangeLog.WarningTextChanged(WarningLevel.Final, true));
    }

    [Fact]
    public void RuleChanged_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AppRuleChangeLog.RuleChanged(null!));
    }
}
