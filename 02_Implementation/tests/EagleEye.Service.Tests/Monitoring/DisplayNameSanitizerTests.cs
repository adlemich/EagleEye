using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class DisplayNameSanitizerTests
{
    [Theory]
    [InlineData("Editor", "Editor")]
    [InlineData("  Visual Studio Code  ", "Visual Studio Code")]
    [InlineData("Ma‮ximum", "Maximum")] // right-to-left override
    [InlineData("Zero​width‍", "Zerowidth")]
    [InlineData("Tab\tNew\nLine\0", "TabNewLine")]
    [InlineData("\u0007\u001B", null)]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("Ärger 😀", "Ärger 😀")]
    public void Sanitize(string? input, string? expected)
    {
        Assert.Equal(expected, DisplayNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_Long_CutAt256()
    {
        Assert.Equal(new string('a', 256), DisplayNameSanitizer.Sanitize(new string('a', 300)));
    }

    [Fact]
    public void Sanitize_SurrogatePairAtLimit_NotSplit()
    {
        var name = new string('a', 255) + "😀" + "bbb";

        Assert.Equal(new string('a', 255), DisplayNameSanitizer.Sanitize(name));
    }
}
