using System.Buffers.Text;
using EagleEye.Service.Pairing;
using Xunit;

namespace EagleEye.Service.Tests.Pairing;

public sealed class PairingTokenServiceTests
{
    private readonly PairingTokenService _service = new();

    [Fact]
    public void CreateToken_Returns32BytesAsBase64Url()
    {
        var token = _service.CreateToken();

        Assert.Equal(32, Base64Url.DecodeFromChars(token).Length);
    }

    [Fact]
    public void CreateToken_UsesUrlSafeAlphabetWithoutPadding()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _service.CreateToken());

        Assert.All(tokens, token => Assert.Matches("^[A-Za-z0-9_-]{43}$", token));
    }

    [Fact]
    public void CreateToken_TwoCalls_ReturnDifferentTokens()
    {
        Assert.NotEqual(_service.CreateToken(), _service.CreateToken());
    }

    [Fact]
    public void Hash_SameToken_IsDeterministic()
    {
        Assert.Equal(_service.Hash("abc"), _service.Hash("abc"));
    }

    [Fact]
    public void Hash_Returns32Bytes()
    {
        Assert.Equal(32, _service.Hash("abc").Length);
    }

    [Fact]
    public void Hash_DifferentTokens_DifferentHashes()
    {
        Assert.NotEqual(_service.Hash("abc"), _service.Hash("abd"));
    }

    [Fact]
    public void Hash_NullToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Hash(null!));
    }
}
