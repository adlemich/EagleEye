using System.Text;
using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class PackageManifestReaderTests
{
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
          <Properties>
            <DisplayName>ms-resource:AppStoreName</DisplayName>
          </Properties>
          <Applications>
            <Application Id="Other" Executable="Other/Helper.exe">
              <uap:VisualElements DisplayName="Helper" />
            </Application>
            <Application Id="App" Executable="CalculatorApp.exe">
              <uap:VisualElements DisplayName="ms-resource:AppName" />
            </Application>
          </Applications>
        </Package>
        """;

    [Fact]
    public void ReadDisplayName_MatchingApplication_ResourceKey()
    {
        Assert.Equal(new ManifestDisplayName("AppName", true), Read(Manifest, "CalculatorApp.exe"));
    }

    [Fact]
    public void ReadDisplayName_MatchingApplicationInSubfolder()
    {
        Assert.Equal(new ManifestDisplayName("Helper", false), Read(Manifest, @"Other\Helper.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Unknown.exe")]
    public void ReadDisplayName_NoMatch_FirstApplication(string? executable)
    {
        Assert.Equal(new ManifestDisplayName("Helper", false), Read(Manifest, executable));
    }

    [Fact]
    public void ReadDisplayName_NoApplications_PropertiesName()
    {
        const string manifest = """<Package><Properties><DisplayName> Contoso Paint </DisplayName></Properties></Package>""";

        Assert.Equal(new ManifestDisplayName("Contoso Paint", false), Read(manifest, "x.exe"));
    }

    [Fact]
    public void ReadDisplayName_VisualElementsOutsideApplication_Ignored()
    {
        const string manifest = """<Package><Properties><DisplayName>Name</DisplayName></Properties><VisualElements DisplayName="Bad" /></Package>""";

        Assert.Equal(new ManifestDisplayName("Name", false), Read(manifest, null));
    }

    [Theory]
    [InlineData("""<Package><Properties><DisplayName>@{evil.dll,-1}</DisplayName></Properties></Package>""")]
    [InlineData("""<Package><Properties><DisplayName>   </DisplayName></Properties></Package>""")]
    [InlineData("""<Package><Properties /></Package>""")]
    [InlineData("""<Package><Applications><Application Executable="a.exe"><VisualElements /></Application></Applications></Package>""")]
    [InlineData("""<!DOCTYPE Package [<!ENTITY x "Evil">]><Package><Properties><DisplayName>&x;</DisplayName></Properties></Package>""")]
    [InlineData("""<Package><Properties><DisplayName>unclosed""")]
    [InlineData("not xml at all")]
    public void ReadDisplayName_InvalidOrDangerous_IsNull(string manifest)
    {
        Assert.Null(Read(manifest, null));
    }

    [Fact]
    public void ReadDisplayName_TooLarge_IsNull()
    {
        var manifest = "<Package><!--" + new string('x', PackageManifestReader.MaxManifestBytes) + "--></Package>";

        Assert.Null(Read(manifest, null));
    }

    [Fact]
    public void ReadDisplayName_NotSeekable_IsNull()
    {
        Assert.Null(PackageManifestReader.ReadDisplayName(new NonSeekableStream(), null));
    }

    [Theory]
    [InlineData("AppName", "@{Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe?ms-resource://Microsoft.WindowsCalculator/Resources/AppName}")]
    [InlineData("Resources/AppName", "@{Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe?ms-resource://Microsoft.WindowsCalculator/Resources/AppName}")]
    [InlineData("/Resources/AppName", "@{Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe?ms-resource://Microsoft.WindowsCalculator/Resources/AppName}")]
    [InlineData("//Other/Resources/AppName", "@{Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe?ms-resource://Other/Resources/AppName}")]
    public void BuildResourceReference(string key, string expected)
    {
        Assert.Equal(expected, PackageManifestReader.BuildResourceReference("Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe", key));
    }

    [Theory]
    [InlineData("NoUnderscore", "AppName")]
    [InlineData("_Leading", "AppName")]
    [InlineData("Evil}_1_x64__abc", "AppName")]
    [InlineData("Evil?_1_x64__abc", "AppName")]
    [InlineData("Microsoft.App_1_x64__abc", "")]
    [InlineData("Microsoft.App_1_x64__abc", "App}Name")]
    [InlineData("Microsoft.App_1_x64__abc", "App{Name")]
    [InlineData("Microsoft.App_1_x64__abc", "App?Name")]
    [InlineData("Microsoft.App_1_x64__abc", "@App")]
    [InlineData("Microsoft.App_1_x64__abc", "App\u0001Name")]
    public void BuildResourceReference_UnsafeCharacters_IsNull(string fullName, string key)
    {
        Assert.Null(PackageManifestReader.BuildResourceReference(fullName, key));
    }

    [Fact]
    public void BuildResourceReference_KeyTooLong_IsNull()
    {
        Assert.Null(PackageManifestReader.BuildResourceReference("Microsoft.App_1_x64__abc", new string('k', 257)));
    }

    [Fact]
    public void Guards_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PackageManifestReader.ReadDisplayName(null!, null));
        Assert.Throws<ArgumentNullException>(() => PackageManifestReader.BuildResourceReference(null!, "k"));
        Assert.Throws<ArgumentNullException>(() => PackageManifestReader.BuildResourceReference("a_b", null!));
    }

    private static ManifestDisplayName? Read(string manifest, string? executable) =>
        PackageManifestReader.ReadDisplayName(new MemoryStream(Encoding.UTF8.GetBytes(manifest)), executable);

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
