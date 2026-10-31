using EagleEye.Shared.Constants;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class AppGroupNamesTests
{
    [Theory]
    [InlineData("Games")]
    [InlineData("  Games  ")]
    [InlineData("A")]
    [InlineData("Spiele für Anna ✨")]
    public void IsValid_NormalNames_True(string name)
    {
        Assert.True(AppGroupNames.IsValid(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ga\u0007mes")]
    [InlineData("Ga\nmes")]
    public void IsValid_EmptyOrControl_False(string? name)
    {
        Assert.False(AppGroupNames.IsValid(name));
    }

    [Fact]
    public void IsValid_LoneSurrogates_False()
    {
        Assert.False(AppGroupNames.IsValid("Bad " + (char)0xD83D));
        Assert.False(AppGroupNames.IsValid((char)0xDE00 + " bad"));
    }

    [Fact]
    public void IsValid_FortyCharacters_True_FortyOne_False()
    {
        Assert.True(AppGroupNames.IsValid(new string('x', 40)));
        Assert.False(AppGroupNames.IsValid(new string('x', 41)));
    }

    [Fact]
    public void IsValid_FortyEmojis_CountAsFortyCharacters()
    {
        var forty = string.Concat(Enumerable.Repeat("🎮", 40));

        Assert.True(AppGroupNames.IsValid(forty));
        Assert.False(AppGroupNames.IsValid(forty + "🎮"));
    }

    [Fact]
    public void IsValid_ZwjFamilyEmoji_CountsOnce()
    {
        Assert.True(AppGroupNames.IsValid("👨‍👩‍👧‍👦"));
    }

    [Fact]
    public void IsValid_TooManyCodeUnits_False()
    {
        // 40 text elements, but each one is a ZWJ sequence of 11 code units: more than MaxCodeUnits.
        var name = string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 40));

        Assert.False(AppGroupNames.IsValid(name));
    }

    [Fact]
    public void Normalize_TrimsAndComposes()
    {
        Assert.Equal("Ä", AppGroupNames.Normalize("  A\u0308 "));
        Assert.Equal(string.Empty, AppGroupNames.Normalize(null));
    }

    [Fact]
    public void Normalize_LoneSurrogate_TrimmedOnly()
    {
        Assert.Equal("x" + (char)0xD83D, AppGroupNames.Normalize(" x" + (char)0xD83D + " "));
    }

    [Theory]
    [InlineData("games", "Games")]
    [InlineData("ä", "Ä")]
    [InlineData(" A\u0308", "ä")]
    public void Key_IgnoresCaseAndForm(string a, string b)
    {
        Assert.Equal(AppGroupNames.Key(a), AppGroupNames.Key(b));
    }

    [Fact]
    public void Key_DifferentNames_Differ()
    {
        Assert.NotEqual(AppGroupNames.Key("Games"), AppGroupNames.Key("Game"));
    }
}
