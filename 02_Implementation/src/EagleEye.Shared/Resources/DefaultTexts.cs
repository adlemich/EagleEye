using System.Globalization;
using System.Resources;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Shared.Resources;

/// <summary>
/// The default kid texts (ADR-016): break display text, budget text, early and final warning. German is the neutral
/// language (<c>Resources/DefaultTexts.resx</c>), English is a satellite (<c>DefaultTexts.en.resx</c>). The app that
/// shows a text renders its default in its own UI language; the service stores only the parent's own texts.
/// </summary>
public static class DefaultTexts
{
    /// <summary>Resource manager for the default texts (exposed for completeness tests).</summary>
    internal static readonly ResourceManager ResourceManager =
        new("EagleEye.Shared.Resources.DefaultTexts", typeof(DefaultTexts).Assembly);

    /// <summary>The languages that have default texts: German (neutral) and English.</summary>
    public static IReadOnlyList<CultureInfo> SupportedCultures { get; } =
        [CultureInfo.GetCultureInfo("de"), CultureInfo.GetCultureInfo("en")];

    /// <summary>The default text of <paramref name="kind"/> in <paramref name="culture"/> (other languages fall back to German).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Unknown kind.</exception>
    public static string Get(DefaultTextKind kind, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown default text kind.");
        }

        return Get(kind.ToString(), culture);
    }

    /// <summary>The resource text of <paramref name="name"/> in <paramref name="culture"/>.</summary>
    /// <exception cref="InvalidOperationException">The resource key does not exist.</exception>
    internal static string Get(string name, CultureInfo culture) =>
        ResourceManager.GetString(name, culture) ?? throw new InvalidOperationException($"Missing default text resource '{name}'.");

    /// <summary>
    /// Whether <paramref name="text"/> (line breaks normalized to "\n") equals the default of <paramref name="kind"/>
    /// in any supported language (ADR-016 §4). The comparison is exact (ordinal, no trimming).
    /// </summary>
    public static bool IsAnyDefault(DefaultTextKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = BreakTimeRules.NormalizeLineBreaks(text);
        return SupportedCultures.Any(culture => string.Equals(Get(kind, culture), normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether a parent text means "the default" (ADR-016 §4): empty, white space only, or equal to the default of any
    /// supported language.
    /// </summary>
    public static bool MeansDefault(DefaultTextKind kind, string? text)
    {
        return string.IsNullOrWhiteSpace(text) || IsAnyDefault(kind, text);
    }
}
