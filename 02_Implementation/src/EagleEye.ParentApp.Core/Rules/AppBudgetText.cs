using System.Globalization;
using EagleEye.Shared.Constants;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// Parsing of the hours and minutes fields of an app budget (US-006 AC-8): ASCII digits only, trimmed, at most two
/// digits; hours 0 … 23, minutes 0 … 59. "", "-1", "1.5", "abc", "24", "60" and non-ASCII digits are rejected. Pure.
/// </summary>
public static class AppBudgetText
{
    private const int MaxDigits = 2;

    /// <summary>Parses the hours field (0 … 23).</summary>
    public static bool TryParseHours(string? text, out int hours) => TryParse(text, 0, AppRuleRules.MaxHours, out hours);

    /// <summary>Parses the minutes field (0 … 59).</summary>
    public static bool TryParseMinutes(string? text, out int minutes) => TryParse(text, 0, AppRuleRules.MaxMinutes, out minutes);

    /// <summary>A field value as shown: no leading zeros.</summary>
    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses a whole number of at most two ASCII digits within the range.</summary>
    internal static bool TryParse(string? text, int min, int max, out int value)
    {
        value = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxDigits || !trimmed.All(char.IsAsciiDigit))
        {
            return false;
        }

        value = int.Parse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture);
        return value >= min && value <= max;
    }
}
