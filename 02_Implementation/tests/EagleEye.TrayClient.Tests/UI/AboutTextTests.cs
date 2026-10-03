using System.Globalization;
using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class AboutTextTests
{
    [Theory]
    [InlineData("de-DE", "Server-Version: EagleEye_v0.1")]
    [InlineData("en-US", "Server Version: EagleEye_v0.1")]
    public void Compose_WithVersion_ShowsServerVersion(string culture, string expected)
    {
        var text = InCulture(culture, () => AboutText.Compose("EagleEye_v0.1", "localhost:5080"));

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("de-DE", "Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich.")]
    [InlineData("en-US", "Connection error: could not connect to the server at localhost:5080.")]
    public void Compose_WithoutVersion_ShowsConnectionErrorWithAddress(string culture, string expected)
    {
        var text = InCulture(culture, () => AboutText.Compose(null, "localhost:5080"));

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Compose_MissingAddress_ThrowsArgumentException(string? serverAddress)
    {
        Assert.ThrowsAny<ArgumentException>(() => AboutText.Compose("EagleEye_v0.1", serverAddress!));
    }

    private static string InCulture(string culture, Func<string> compose)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return compose();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = original;
        }
    }
}
