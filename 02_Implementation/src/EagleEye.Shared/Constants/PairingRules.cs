namespace EagleEye.Shared.Constants;

/// <summary>
/// Pairing rules shared by the service and the parent app (FR-SVC-090 to FR-SVC-095, ADR-008).
/// </summary>
public static class PairingRules
{
    /// <summary>Number of digits of a pairing code.</summary>
    public const int CodeLength = 6;

    /// <summary>Maximum length of a device name, after trimming.</summary>
    public const int DeviceNameMaxLength = 50;

    /// <summary>How long a pairing code is valid (FR-SVC-093).</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Whether <paramref name="code"/> consists of exactly <see cref="CodeLength"/> ASCII digits.</summary>
    public static bool IsValidCodeFormat(string? code)
    {
        return code is { Length: CodeLength } && code.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Returns the trimmed device name, or <c>null</c> if it is empty, longer than
    /// <see cref="DeviceNameMaxLength"/> or contains control characters.
    /// </summary>
    public static string? NormalizeDeviceName(string? deviceName)
    {
        var trimmed = deviceName?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > DeviceNameMaxLength || trimmed.Any(char.IsControl))
        {
            return null;
        }

        return trimmed;
    }
}
