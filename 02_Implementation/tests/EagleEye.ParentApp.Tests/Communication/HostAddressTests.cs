using EagleEye.ParentApp.Core.Communication;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

[Collection("Environment variables")]
public sealed class HostAddressTests
{
    [Theory]
    [InlineData("kid-pc", "kid-pc", "https://kid-pc:5443/hubs/parent")]
    [InlineData("KID-PC", "KID-PC", "https://kid-pc:5443/hubs/parent")]
    [InlineData("kid-pc.fritz.box", "kid-pc.fritz.box", "https://kid-pc.fritz.box:5443/hubs/parent")]
    [InlineData("kid-pc.fritz.box.", "kid-pc.fritz.box.", "https://kid-pc.fritz.box.:5443/hubs/parent")]
    [InlineData("pc2", "pc2", "https://pc2:5443/hubs/parent")]
    [InlineData("  kid-pc  ", "kid-pc", "https://kid-pc:5443/hubs/parent")]
    [InlineData("192.168.178.20", "192.168.178.20", "https://192.168.178.20:5443/hubs/parent")]
    [InlineData("fe80::1", "fe80::1", "https://[fe80::1]:5443/hubs/parent")]
    [InlineData("::1", "::1", "https://[::1]:5443/hubs/parent")]
    [InlineData("::ffff:192.168.1.2", "::ffff:192.168.1.2", "https://[::ffff:192.168.1.2]:5443/hubs/parent")]
    public void TryParse_ValidHost_ReturnsHostWithDisplayAndUri(string input, string display, string uri)
    {
        var ok = HostAddress.TryParse(input, out var host);

        Assert.True(ok);
        Assert.Equal((display, new Uri(uri)), (host!.Display, host.ToParentHubUri()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://kid-pc")]
    [InlineData("kid-pc:5443")]
    [InlineData("192.168.1.2:5443")]
    [InlineData("[::1]")]
    [InlineData("[::1]:5443")]
    [InlineData("fe80::1%12")]
    [InlineData("kid-pc/hubs")]
    [InlineData("kid pc")]
    [InlineData("-kid")]
    [InlineData("kid-")]
    [InlineData("kid..pc")]
    [InlineData("kid_pc")]
    [InlineData("kinder-pc-ä")]
    [InlineData("1.2.3")]
    [InlineData("1")]
    [InlineData("999.1.1.1")]
    [InlineData("0x7f.0.0.1")]
    public void TryParse_InvalidHost_ReturnsFalse(string? input)
    {
        var ok = HostAddress.TryParse(input, out var host);

        Assert.False(ok);
        Assert.Null(host);
    }

    [Fact]
    public void TryParse_LabelLongerThan63_ReturnsFalse()
    {
        Assert.False(HostAddress.TryParse(new string('a', 64), out _));
    }

    [Fact]
    public void TryParse_Label63_ReturnsTrue()
    {
        Assert.True(HostAddress.TryParse(new string('a', 63), out _));
    }

    [Fact]
    public void TryParse_HostLongerThan253_ReturnsFalse()
    {
        var label = new string('a', 60);
        var host = string.Join('.', label, label, label, label, "abcdefghij");

        Assert.False(HostAddress.TryParse(host, out _));
    }

    [Fact]
    public void ToString_ReturnsDisplay()
    {
        HostAddress.TryParse(" kid-pc ", out var host);

        Assert.Equal("kid-pc", host!.ToString());
    }
}
