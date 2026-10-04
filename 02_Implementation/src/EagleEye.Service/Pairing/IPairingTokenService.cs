namespace EagleEye.Service.Pairing;

/// <summary>Creates and hashes device tokens.</summary>
public interface IPairingTokenService
{
    /// <summary>Returns a new random token (256 bit, Base64Url).</summary>
    string CreateToken();

    /// <summary>Returns the SHA-256 hash of the token (32 bytes).</summary>
    byte[] Hash(string token);
}
