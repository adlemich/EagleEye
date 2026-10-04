using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EagleEye.Service.Pairing;

/// <summary>Device tokens: 32 random bytes, Base64Url; stored only as SHA-256 hash (ADR-004).</summary>
public sealed class PairingTokenService : IPairingTokenService
{
    private const int TokenBytes = 32;

    /// <inheritdoc />
    public string CreateToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
    }

    /// <inheritdoc />
    public byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
