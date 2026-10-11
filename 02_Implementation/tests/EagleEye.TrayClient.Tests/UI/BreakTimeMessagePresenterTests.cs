using EagleEye.Shared.Models;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class BreakTimeMessagePresenterTests
{
    private readonly List<(string Text, Action Closed)> _shown = [];
    private readonly BreakTimeMessagePresenter _presenter;

    public BreakTimeMessagePresenterTests()
    {
        _presenter = new BreakTimeMessagePresenter((text, closed) => _shown.Add((text, closed)));
    }

    [Fact]
    public void Present_First_ShownWithPlatformLineBreaksAndEmojis()
    {
        var result = _presenter.Present("Hallo \U0001F60A\nZeile 2");

        Assert.Equal(KidMessageResult.Shown, result);
        Assert.Equal("Hallo \U0001F60A" + Environment.NewLine + "Zeile 2", _shown.Single().Text);
    }

    [Fact]
    public void Present_WhileOpen_AlreadyOpenAndNothingShown()
    {
        _presenter.Present("a");

        Assert.Equal(KidMessageResult.AlreadyOpen, _presenter.Present("b"));
        Assert.Single(_shown);
    }

    [Fact]
    public void Present_AfterOk_ShownAgain()
    {
        _presenter.Present("a");
        _shown[0].Closed();

        Assert.Equal(KidMessageResult.Shown, _presenter.Present("b"));
        Assert.Equal(2, _shown.Count);
    }

    [Fact]
    public void Present_ConcurrentRequests_OnlyOneShown()
    {
        var results = new KidMessageResult[20];
        Parallel.For(0, results.Length, i => results[i] = _presenter.Present("x"));

        Assert.Equal(1, results.Count(r => r == KidMessageResult.Shown));
    }

    [Theory]
    [InlineData("a\r\nb", "a{0}b")]
    [InlineData("a\nb\n", "a{0}b{0}")]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467 ❤️", "\U0001F468‍\U0001F469‍\U0001F467 ❤️")]
    public void ToDisplay_OnlyLineBreaksChange(string input, string expected)
    {
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, Environment.NewLine), BreakTimeMessagePresenter.ToDisplay(input));
    }

    [Fact]
    public void Present_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _presenter.Present(null!));
    }
}
