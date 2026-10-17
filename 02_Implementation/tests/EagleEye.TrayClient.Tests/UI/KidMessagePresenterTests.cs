using EagleEye.Shared.Models;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class KidMessagePresenterTests
{
    private static readonly KidMessageDto Message = new(KidMessageReason.AppEarlyWarning, "Hallo \U0001F60A\nZeile 2", "Editor", 2, null, null);

    private readonly List<KidMessageContent> _shown = [];
    private readonly List<KidMessageContent> _replaced = [];
    private readonly KidMessagePresenter _presenter;

    public KidMessagePresenterTests()
    {
        _presenter = new KidMessagePresenter(_shown.Add, _replaced.Add, m => new KidMessageContent(m.ParentText!, "info"));
    }

    [Fact]
    public void Present_First_ShownWithPlatformLineBreaksAndEmojis()
    {
        var result = _presenter.Present(Message);

        Assert.Equal(KidMessageResult.Shown, result);
        Assert.Equal(new KidMessageContent("Hallo \U0001F60A" + Environment.NewLine + "Zeile 2", "info"), _shown.Single());
    }

    [Fact]
    public void Present_WhileOpen_TextReplaced()
    {
        _presenter.Present(Message);

        Assert.Equal(KidMessageResult.Replaced, _presenter.Present(Message with { ParentText = "b" }));
        Assert.Single(_shown);
        Assert.Equal("b", _replaced.Single().Text);
    }

    [Fact]
    public void Present_AfterOk_ShownAgain()
    {
        _presenter.Present(Message);
        _presenter.DialogClosed();

        Assert.Equal(KidMessageResult.Shown, _presenter.Present(Message));
        Assert.Equal(2, _shown.Count);
    }

    [Fact]
    public void DialogReopened_NextMessageReplaces()
    {
        _presenter.Present(Message);
        _presenter.DialogClosed();
        _presenter.DialogReopened();

        Assert.Equal(KidMessageResult.Replaced, _presenter.Present(Message));
    }

    [Fact]
    public void Present_ConcurrentRequests_OneShownTheOthersReplace()
    {
        var presenter = new KidMessagePresenter(_ => { }, _ => { }, m => new KidMessageContent("x", "i"));
        var results = new KidMessageResult[20];
        Parallel.For(0, results.Length, i => results[i] = presenter.Present(Message));

        Assert.Equal((1, 19), (results.Count(r => r == KidMessageResult.Shown), results.Count(r => r == KidMessageResult.Replaced)));
    }

    [Fact]
    public void DefaultConstructor_RendersWithTheCurrentCultures()
    {
        KidMessageContent? shown = null;
        var presenter = new KidMessagePresenter(c => shown = c, _ => { });

        presenter.Present(Message with { ParentText = null });

        Assert.False(string.IsNullOrEmpty(shown!.Text));
        Assert.Contains("Editor", shown.Info, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a\r\nb", "a{0}b")]
    [InlineData("a\nb\n", "a{0}b{0}")]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467 ❤️", "\U0001F468‍\U0001F469‍\U0001F467 ❤️")]
    public void ToDisplay_OnlyLineBreaksChange(string input, string expected)
    {
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, Environment.NewLine), KidMessagePresenter.ToDisplay(input));
    }

    [Fact]
    public void Present_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _presenter.Present(null!));
    }
}
