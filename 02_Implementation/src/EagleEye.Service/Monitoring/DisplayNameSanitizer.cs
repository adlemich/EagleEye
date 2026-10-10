using System.Globalization;
using System.Text;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Cleans display names read from kid-controlled files (ADR-011 §7 item 16, T-1): removes control and format
/// characters (incl. bidi overrides and zero-width characters), trims, and cuts at <see cref="MaxLength"/>
/// characters without splitting a surrogate pair. An empty result is <c>null</c>.
/// </summary>
public static class DisplayNameSanitizer
{
    /// <summary>Maximum length of a display name.</summary>
    public const int MaxLength = 256;

    /// <summary>Returns the cleaned name, or <c>null</c> if nothing is left.</summary>
    public static string? Sanitize(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is not (UnicodeCategory.Control or UnicodeCategory.Format))
            {
                builder.Append(c);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length > MaxLength)
        {
            var cut = char.IsHighSurrogate(cleaned[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            cleaned = cleaned[..cut].TrimEnd();
        }

        return cleaned.Length == 0 ? null : cleaned;
    }
}
