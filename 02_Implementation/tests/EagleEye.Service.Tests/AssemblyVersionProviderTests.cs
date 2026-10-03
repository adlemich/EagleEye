using System.Reflection;
using System.Reflection.Emit;
using EagleEye.Service;
using Xunit;

namespace EagleEye.Service.Tests;

public sealed class AssemblyVersionProviderTests
{
    [Fact]
    public void GetVersion_ServiceAssembly_ReturnsFormattedVersionString()
    {
        var provider = new AssemblyVersionProvider(typeof(AssemblyVersionProvider).Assembly);

        var version = provider.GetVersion();

        Assert.Matches(@"^EagleEye_v\d+\.\d+$", version.Version);
    }

    [Fact]
    public void GetVersion_AssemblyWithoutInformationalVersion_ReturnsFallback()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("NoVersionAttribute"), AssemblyBuilderAccess.Run);
        var provider = new AssemblyVersionProvider(assembly);

        var version = provider.GetVersion();

        Assert.Equal("EagleEye_v0.0", version.Version);
    }

    [Fact]
    public void Constructor_NullAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AssemblyVersionProvider(null!));
    }

    [Theory]
    [InlineData("0.1.0", "EagleEye_v0.1")]
    [InlineData("0.1.0+3161f70abc", "EagleEye_v0.1")]
    [InlineData("1.2.3-beta.1", "EagleEye_v1.2")]
    [InlineData("10.20", "EagleEye_v10.20")]
    public void Format_ValidVersion_ReturnsMajorMinor(string informationalVersion, string expected)
    {
        var formatted = AssemblyVersionProvider.Format(informationalVersion);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void Format_MissingOrInvalidVersion_ReturnsFallback(string? informationalVersion)
    {
        var formatted = AssemblyVersionProvider.Format(informationalVersion);

        Assert.Equal("EagleEye_v0.0", formatted);
    }
}
