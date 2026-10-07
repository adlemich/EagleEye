using EagleEye.Shared.Constants;
using Xunit;

namespace EagleEye.Shared.Tests.Constants;

public sealed class PairingRulesTests
{
    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("999999")]
    public void IsValidCodeFormat_SixDigits_ReturnsTrue(string code)
    {
        Assert.True(PairingRules.IsValidCodeFormat(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData("123 56")]
    [InlineData(" 123456")]
    [InlineData("１２３４５６")]
    public void IsValidCodeFormat_InvalidCode_ReturnsFalse(string? code)
    {
        Assert.False(PairingRules.IsValidCodeFormat(code));
    }

    [Theory]
    [InlineData("Dad's laptop", "Dad's laptop")]
    [InlineData("  Dad's laptop  ", "Dad's laptop")]
    [InlineData("A", "A")]
    public void NormalizeDeviceName_ValidName_ReturnsTrimmedName(string input, string expected)
    {
        Assert.Equal(expected, PairingRules.NormalizeDeviceName(input));
    }

    [Fact]
    public void NormalizeDeviceName_FiftyCharacters_ReturnsName()
    {
        var name = new string('x', 50);

        Assert.Equal(name, PairingRules.NormalizeDeviceName(name));
    }

    [Fact]
    public void NormalizeDeviceName_FiftyCharactersWithSurroundingSpaces_ReturnsTrimmedName()
    {
        var name = new string('x', 50);

        Assert.Equal(name, PairingRules.NormalizeDeviceName("  " + name + "  "));
    }

    [Fact]
    public void NormalizeDeviceName_FiftyOneCharacters_ReturnsNull()
    {
        Assert.Null(PairingRules.NormalizeDeviceName(new string('x', 51)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("Dad\nlaptop")]
    [InlineData("Dad\u0000laptop")]
    [InlineData("Dad\u007Flaptop")]
    public void NormalizeDeviceName_EmptyOrControlCharacters_ReturnsNull(string? input)
    {
        Assert.Null(PairingRules.NormalizeDeviceName(input));
    }

    [Fact]
    public void CodeLifetime_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), PairingRules.CodeLifetime);
    }
}
