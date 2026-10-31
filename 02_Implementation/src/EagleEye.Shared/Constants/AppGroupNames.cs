using System.Globalization;
using System.Text;

namespace EagleEye.Shared.Constants;

/// <summary>
/// Name rules of app groups, shared by the service and the parent app (US-008 AC-8, TI-7): a name is normalized (Unicode
/// NFC, trimmed) and then has 1 to <see cref="MaxLength"/> characters, counted as text elements so that an emoji counts
/// once; no control characters, no lone surrogates. Two names are equal ignoring case (<see cref="Key"/>).
/// </summary>
public static class AppGroupNames
{
    /// <summary>The largest number of characters (text elements) of a name.</summary>
    public const int MaxLength = 40;

    /// <summary>The largest number of UTF-16 code units of a name (bounds emoji sequences of many code units).</summary>
    public const int MaxCodeUnits = 160;

    /// <summary>The name in normalized form: NFC, trimmed. A name with lone surrogates is returned trimmed only.</summary>
    public static string Normalize(string? name)
    {
        var text = name ?? string.Empty;
        return (!HasValidSurrogates(text) || text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC)).Trim();
    }

    /// <summary>Whether the normalized <paramref name="name"/> is a valid group name.</summary>
    public static bool IsValid(string? name)
    {
        var normalized = Normalize(name);
        return normalized.Length is > 0 and <= MaxCodeUnits
            && HasValidSurrogates(normalized)
            && !normalized.Any(char.IsControl)
            && new StringInfo(normalized).LengthInTextElements <= MaxLength;
    }

    /// <summary>The comparison key of a name: unique ignoring case, also for non-ASCII letters ("Ä" = "ä").</summary>
    public static string Key(string? name) => Normalize(name).ToUpperInvariant();

    private static bool HasValidSurrogates(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
