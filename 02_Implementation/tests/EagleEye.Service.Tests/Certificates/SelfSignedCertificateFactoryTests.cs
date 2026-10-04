using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EagleEye.Service.Certificates;
using Xunit;

namespace EagleEye.Service.Tests.Certificates;

public sealed class SelfSignedCertificateFactoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly X509Certificate2 _certificate = new SelfSignedCertificateFactory().Create("kid-pc", Now);

    public void Dispose() => _certificate.Dispose();

    [Fact]
    public void Create_SubjectIsEagleEye()
    {
        Assert.Equal("CN=EagleEye", _certificate.Subject);
    }

    [Fact]
    public void Create_SubjectAlternativeNames_ContainMachineAndLocalhost()
    {
        var extension = _certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();

        Assert.Equal(["kid-pc", "localhost"], extension.EnumerateDnsNames().ToArray());
    }

    [Fact]
    public void Create_EnhancedKeyUsage_IsServerAuthentication()
    {
        var eku = _certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();

        Assert.Equal(["1.3.6.1.5.5.7.3.1"], eku.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).ToArray());
    }

    [Fact]
    public void Create_Validity_IsAtLeast99Years()
    {
        Assert.True(_certificate.NotAfter - _certificate.NotBefore >= TimeSpan.FromDays(99 * 365));
    }

    [Fact]
    public void Create_Validity_StartsBeforeNow()
    {
        Assert.True(_certificate.NotBefore.ToUniversalTime() <= Now.UtcDateTime);
    }

    [Fact]
    public void Create_HasPrivateKey()
    {
        Assert.True(_certificate.HasPrivateKey);
    }

    [Fact]
    public void Create_KeyIsEcdsaP256()
    {
        using var key = _certificate.GetECDsaPublicKey();

        Assert.NotNull(key);
        Assert.Equal(256, key.KeySize);
    }

    [Fact]
    public void Create_IsNotACertificateAuthority()
    {
        var constraints = _certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single();

        Assert.False(constraints.CertificateAuthority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_MissingDnsName_ThrowsArgumentException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SelfSignedCertificateFactory().Create(name!, Now));
    }
}
