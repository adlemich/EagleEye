using System.Globalization;
using EagleEye.Shared.Models;
using EagleEye.TrayClient.Overlay;
using Xunit;

namespace EagleEye.TrayClient.Tests.Overlay;

public sealed class OverlayLayoutTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(15, 899, true)]
    [InlineData(15, 900, false)]
    [InlineData(30, 1799, true)]
    [InlineData(30, 1800, false)]
    [InlineData(45, 2699, true)]
    [InlineData(45, 2700, false)]
    [InlineData(60, 3599, true)]
    [InlineData(60, 3600, false)]
    public void Due_BelowTheThreshold(int threshold, int seconds, bool due)
    {
        var layout = OverlayLayout.Build([App("Editor", seconds)], threshold, 5, 1, English);

        Assert.Equal(due, layout.Lines.Count == 1);
    }

    [Fact]
    public void Due_EarlyLevelAboveTheThreshold_Shown()
    {
        var layout = OverlayLayout.Build([App("Editor", 2700), App("Paint", 2701)], 15, 45, 1, English);

        Assert.Equal(new PillLine("Editor 45:00", OverlayLevel.Early, true), layout.Lines.Single());
    }

    [Fact]
    public void Sort_ShortestFirst_TiesBreakThenNamesIgnoringCaseThenOrdinal_TopLineLarge()
    {
        DisplayedLine[] lines = [App("zeta", 600), App("Beta", 300), Break(300), App("alpha", 300), App("Alpha", 300), App("Games", 252)];

        var layout = OverlayLayout.Build(lines, 30, 5, 1, English);

        Assert.Equal(["Games 4:12", "Break in 5:00", "Alpha 5:00", "+2 more"], layout.Lines.Take(3).Select(l => l.Text).Append(layout.Lines[^1].Text));
        Assert.Equal([true, false, false, false, false], layout.Lines.Select(l => l.IsTop));
    }

    [Fact]
    public void Sort_SameNameDifferentCase_OrdinalDecides()
    {
        var layout = OverlayLayout.Build([App("alpha", 300), App("Alpha", 300)], 30, 5, 1, English);

        Assert.Equal(["Alpha 5:00", "alpha 5:00"], layout.Lines.Select(l => l.Text));
    }

    [Fact]
    public void Cap_FiveLinesShown_SixDue_FourAndPlusTwo_NotDueNeverCounted()
    {
        DisplayedLine[] lines = [App("a", 100), App("b", 200), App("c", 300), App("d", 400), App("e", 500), App("f", 600), App("g", 1800)];

        var layout = OverlayLayout.Build(lines, 30, 5, 1, German);

        Assert.Equal(["a 1:40", "b 3:20", "c 5:00", "d 6:40", "+2 weitere"], layout.Lines.Select(l => l.Text));
        Assert.Equal(OverlayLevel.Normal, layout.Lines[^1].Level);
    }

    [Fact]
    public void Cap_ExactlyFive_NoMoreLine()
    {
        var layout = OverlayLayout.Build([App("a", 1), App("b", 2), App("c", 3), App("d", 4), App("e", 5)], 30, 5, 1, English);

        Assert.Equal(5, layout.Lines.Count);
        Assert.Equal("e 0:05", layout.Lines[^1].Text);
    }

    [Theory]
    [InlineData("de-DE", "Ruhezeit in 18:30")]
    [InlineData("en-US", "Break in 18:30")]
    [InlineData("fr-FR", "Ruhezeit in 18:30")]
    public void BreakText_GermanEnglish(string culture, string expected)
    {
        var layout = OverlayLayout.Build([Break(1110)], 30, 5, 1, CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, layout.Lines.Single().Text);
    }

    [Theory]
    [InlineData(-5, "0:00")]
    [InlineData(0, "0:00")]
    [InlineData(42, "0:42")]
    [InlineData(899, "14:59")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(6300, "1:45:00")]
    public void FormatTime_MinutesSeconds_HoursFromOneHour(int seconds, string expected)
    {
        Assert.Equal(expected, OverlayLayout.FormatTime(seconds));
    }

    [Fact]
    public void ShortName_LongerThan24_Cut_EmojiNeverHalved()
    {
        Assert.Equal("Minecraft Launcher Beta", OverlayLayout.ShortName("Minecraft Launcher Beta"));
        Assert.Equal(new string('x', 24), OverlayLayout.ShortName(new string('x', 24)));
        Assert.Equal(new string('x', 23) + "…", OverlayLayout.ShortName(new string('x', 25)));
        var withEmoji = new string('x', 22) + "\U0001F3AE\U0001F3AE" + "yy";
        Assert.Equal(new string('x', 22) + "\U0001F3AE…", OverlayLayout.ShortName(withEmoji));
        Assert.Equal("Spiele 🎮 0:42", OverlayLayout.Build([App("Spiele 🎮", 42)], 30, 5, 1, German).Lines.Single().Text);
    }

    [Fact]
    public void Levels_PerLine_AnyFinalAboveZeroOnlyForShownRedLines()
    {
        var red = OverlayLayout.Build([App("a", 0), App("b", 60), App("c", 300), App("d", 301)], 30, 5, 1, English);
        var expiredOnly = OverlayLayout.Build([App("a", 0)], 30, 5, 1, English);

        Assert.Equal([OverlayLevel.Expired, OverlayLevel.Final, OverlayLevel.Early, OverlayLevel.Normal], red.Lines.Select(l => l.Level));
        Assert.True(red.AnyFinalAboveZero);
        Assert.False(expiredOnly.AnyFinalAboveZero);
    }

    [Fact]
    public void NothingDue_Empty()
    {
        var layout = OverlayLayout.Build([App("a", 3000)], 30, 5, 1, English);

        Assert.Empty(layout.Lines);
        Assert.False(layout.AnyFinalAboveZero);
    }

    [Fact]
    public void NullName_EmptyName()
    {
        var layout = OverlayLayout.Build([new DisplayedLine(OverlayLineKind.App, null, 30)], 30, 5, 1, English);

        Assert.Equal(" 0:30", layout.Lines.Single().Text);
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => OverlayLayout.Build(null!, 30, 5, 1, English));
        Assert.Throws<ArgumentNullException>(() => OverlayLayout.Build([], 30, 5, 1, null!));
        Assert.Throws<ArgumentNullException>(() => OverlayLayout.ShortName(null!));
    }

    private static DisplayedLine App(string name, int seconds) => new(OverlayLineKind.App, name, seconds);

    private static DisplayedLine Break(int seconds) => new(OverlayLineKind.Break, null, seconds);
}
