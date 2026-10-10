using EagleEye.Service.Monitoring;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class AppNameResolverTests
{
    private static readonly ProcessFacts Game = new(10, 2, "S-1-5-21-1-2-3-1003", 1, @"C:\Users\kid\Games\mygame.EXE", null);

    private readonly Mock<IAppMetadataSource> _metadata = new();
    private readonly TestLogger<AppNameResolver> _logger = new();
    private readonly AppNameResolver _resolver;

    public AppNameResolverTests()
    {
        _resolver = new AppNameResolver(_metadata.Object, _logger);
    }

    [Fact]
    public void Resolve_PackageNameWins()
    {
        _metadata.Setup(m => m.Read(Game)).Returns(new AppMetadata("Rechner", "CalculatorApp"));

        Assert.Equal("Rechner", _resolver.Resolve(Game));
    }

    [Fact]
    public void Resolve_FileDescriptionWithoutPackage()
    {
        _metadata.Setup(m => m.Read(Game)).Returns(new AppMetadata(null, "Minecraft Launcher"));

        Assert.Equal("Minecraft Launcher", _resolver.Resolve(Game));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", "\t")]
    [InlineData("\u202E", "")]
    public void Resolve_NoUsableName_ProcessNameWithoutExe(string? package, string? description)
    {
        _metadata.Setup(m => m.Read(Game)).Returns(new AppMetadata(package, description));

        Assert.Equal("mygame", _resolver.Resolve(Game));
    }

    [Fact]
    public void Resolve_NamesAreSanitized()
    {
        _metadata.Setup(m => m.Read(Game)).Returns(new AppMetadata(null, " Ed\u202Eitor "));

        Assert.Equal("Editor", _resolver.Resolve(Game));
    }

    [Fact]
    public void Resolve_SourceThrows_ProcessNameAndWarning()
    {
        var failure = new IOException("access denied");
        _metadata.Setup(m => m.Read(Game)).Throws(failure);

        Assert.Equal("mygame", _resolver.Resolve(Game));
        Assert.True(_logger.Has(LogLevel.Warning, failure));
    }

    [Fact]
    public void Resolve_SecondTime_UsesCacheIgnoringCase()
    {
        _metadata.Setup(m => m.Read(It.IsAny<ProcessFacts>())).Returns(new AppMetadata(null, "Game"));
        _resolver.Resolve(Game);

        _resolver.Resolve(Game with { ProcessId = 11, ImagePath = Game.ImagePath.ToUpperInvariant() });

        _metadata.Verify(m => m.Read(It.IsAny<ProcessFacts>()), Times.Once);
    }

    [Fact]
    public void Resolve_OtherPackageSamePath_ResolvedSeparately()
    {
        _metadata.Setup(m => m.Read(It.IsAny<ProcessFacts>())).Returns(new AppMetadata(null, "Game"));
        _resolver.Resolve(Game with { PackageFullName = "A_1" });

        _resolver.Resolve(Game with { PackageFullName = "a_1" });
        _resolver.Resolve(Game with { PackageFullName = "B_1" });

        _metadata.Verify(m => m.Read(It.IsAny<ProcessFacts>()), Times.Exactly(2));
    }

    [Fact]
    public void Resolve_ProcessNameOnlyControlCharacters_QuestionMark()
    {
        _metadata.Setup(m => m.Read(It.IsAny<ProcessFacts>())).Returns(AppMetadata.None);

        Assert.Equal("?", _resolver.Resolve(Game with { ImagePath = "C:\\x\\\u202E.exe" }));
    }

    [Theory]
    [InlineData(@"C:\a\Code.exe", "Code")]
    [InlineData(@"C:\a\tool.EXE", "tool")]
    [InlineData(@"C:\a\tool.com", "tool.com")]
    public void ProcessNameWithoutExtension(string path, string expected)
    {
        Assert.Equal(expected, AppNameResolver.ProcessNameWithoutExtension(path));
    }

    [Fact]
    public void Guards_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(null!));
        Assert.Throws<ArgumentNullException>(() => AppNameResolver.ProcessNameWithoutExtension(null!));
    }
}
