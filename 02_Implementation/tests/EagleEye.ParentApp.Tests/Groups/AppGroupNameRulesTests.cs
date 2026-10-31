using EagleEye.ParentApp.Core.Groups;
using EagleEye.ParentApp.Tests.Fakes;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.Groups;

public sealed class AppGroupNameRulesTests
{
    private static readonly AppGroupDto[] Groups = [new(7, "Games"), new(8, "Äpfel")];

    [Theory]
    [InlineData("de-DE", "", "Bitte einen Namen mit 1 bis 40 Zeichen eingeben.")]
    [InlineData("en-US", "   ", "Enter a name with 1 to 40 characters.")]
    [InlineData("en-US", "12345678901234567890123456789012345678901", "Enter a name with 1 to 40 characters.")]
    [InlineData("de-DE", "games", "Es gibt bereits eine Gruppe mit diesem Namen.")]
    [InlineData("en-US", " äpfel ", "A group with this name already exists.")]
    public void Problem_InvalidOrDuplicate_Message(string culture, string name, string expected)
    {
        Assert.Equal(expected, TestSupport.InCulture(culture, () => AppGroupNameRules.Problem(name, Groups, null)));
    }

    [Theory]
    [InlineData("School", null)]
    [InlineData("GAMES", 7L)]
    [InlineData("🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮🎮", null)]
    public void Problem_Valid_Null(string name, long? renamedGroupId)
    {
        Assert.Null(AppGroupNameRules.Problem(name, Groups, renamedGroupId));
    }

    [Fact]
    public void Problem_NullGroups_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AppGroupNameRules.Problem("x", null!, null));
    }
}
