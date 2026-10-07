using System.Globalization;
using System.Security.Cryptography;

namespace EagleEye.Service.Pairing;

/// <summary>Cryptographically random 6-digit pairing codes (FR-SVC-091).</summary>
public sealed class PairingCodeGenerator : IPairingCodeGenerator
{
    private const int ExclusiveUpperBound = 1_000_000;
    private const string CodeFormat = "D6";

    /// <inheritdoc />
    public string Generate()
    {
        return RandomNumberGenerator.GetInt32(0, ExclusiveUpperBound).ToString(CodeFormat, CultureInfo.InvariantCulture);
    }
}
