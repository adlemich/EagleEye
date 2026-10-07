using Xunit;

namespace EagleEye.Service.Tests;

public sealed class ServicePathsTests
{
    private static readonly string Root = Path.Combine("data", "EagleEye");

    [Fact]
    public void Constructor_DerivesCertificateAndDatabasePaths()
    {
        var paths = new ServicePaths(Root);

        Assert.Equal(
            (Root, Path.Combine(Root, "certs"), Path.Combine(Root, "certs", "eagleeye.pfx"), Path.Combine(Root, "EagleEye.Service.db")),
            (paths.DataDirectory, paths.CertificateDirectory, paths.CertificatePath, paths.DatabasePath));
    }

    [Fact]
    public void Resolve_WithoutOverride_UsesProgramDataEagleEye()
    {
        var paths = WithOverride(null, ServicePaths.Resolve);

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EagleEye");
        Assert.Equal(expected, paths.DataDirectory);
    }

    [Fact]
    public void Resolve_DebugBuildWithOverride_UsesOverrideDirectory()
    {
        var overrideDirectory = Path.Combine(Path.GetTempPath(), "eagleeye-override");

        var paths = WithOverride(overrideDirectory, ServicePaths.Resolve);

        Assert.Equal(overrideDirectory, paths.DataDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingDirectory_ThrowsArgumentException(string? directory)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ServicePaths(directory!));
    }

    private static T WithOverride<T>(string? value, Func<T> action)
    {
        var original = Environment.GetEnvironmentVariable(ServicePaths.DataDirectoryOverrideVariable);
        try
        {
            Environment.SetEnvironmentVariable(ServicePaths.DataDirectoryOverrideVariable, value);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ServicePaths.DataDirectoryOverrideVariable, original);
        }
    }
}
