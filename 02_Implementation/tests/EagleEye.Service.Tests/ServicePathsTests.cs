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
    public void Constructor_DerivesLogDirectoryAndPrefix()
    {
        var paths = new ServicePaths(Root);

        Assert.Equal((Path.Combine(Root, "logs"), "EagleEye.Service", false), (paths.LogDirectory, paths.LogFilePrefix, paths.IsOverridden));
    }

    [Fact]
    public void Constructor_Overridden_IsOverridden()
    {
        Assert.True(new ServicePaths(Root, isOverridden: true).IsOverridden);
    }

    [Fact]
    public void Resolve_WithoutOverride_UsesProgramDataEagleEye()
    {
        var paths = WithOverride(null, ServicePaths.Resolve);

        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EagleEye");
        Assert.Equal((expected, false), (paths.DataDirectory, paths.IsOverridden));
    }

    [Fact]
    public void Resolve_DebugBuildWithOverride_UsesOverrideDirectory()
    {
        var overrideDirectory = Path.Combine(Path.GetTempPath(), "eagleeye-override");

        var paths = WithOverride(overrideDirectory, ServicePaths.Resolve);

        Assert.Equal((overrideDirectory, true), (paths.DataDirectory, paths.IsOverridden));
    }

    [Fact]
    public void EnsureDirectories_CreatesDataCertificateAndLogFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N"));
        var paths = new ServicePaths(root);
        try
        {
            paths.EnsureDirectories();

            Assert.True(Directory.Exists(paths.CertificateDirectory) && Directory.Exists(paths.LogDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
