using EagleEye.Service.Communication;
using Xunit;

namespace EagleEye.Service.Tests.Communication;

public sealed class BearerTokenTests
{
    [Theory]
    [InlineData("Bearer abc-DEF_123", "abc-DEF_123")]
    [InlineData("bearer abc", "abc")]
    [InlineData("Bearer   abc  ", "abc")]
    public void Parse_BearerHeader_ReturnsToken(string header, string expected)
    {
        Assert.Equal(expected, BearerToken.Parse(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer ")]
    [InlineData("Bearer    ")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer")]
    public void Parse_MissingOrOtherScheme_ReturnsNull(string? header)
    {
        Assert.Null(BearerToken.Parse(header));
    }
}
