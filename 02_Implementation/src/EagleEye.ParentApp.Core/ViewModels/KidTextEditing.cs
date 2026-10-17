using System.Globalization;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using EagleEye.Shared.Resources;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// The rules of every kid text editor (break display text, budget text, warning texts; ADR-016): an unchanged default
/// is shown in the parent app's language; leaving the box sends nothing when the text is unchanged, an empty text when
/// it now means the default (empty, white space only, or the default of any language), otherwise the text. Pure.
/// </summary>
public static class KidTextEditing
{
    /// <summary>The text shown in the edit box: the parent's text, or the default in <paramref name="culture"/>.</summary>
    public static string ShownText(string? parentText, DefaultTextKind kind, CultureInfo culture) =>
        parentText ?? DefaultTexts.Get(kind, culture);

    /// <summary>What to send when the parent leaves the box.</summary>
    /// <param name="edited">The text in the box.</param>
    /// <param name="confirmed">The confirmed text as shown (<see cref="ShownText"/>).</param>
    /// <param name="isDefault">Whether the confirmed text is the default.</param>
    /// <param name="kind">The kind of text.</param>
    /// <returns><c>null</c> = nothing to send; empty = restore the default; otherwise the new text (line breaks "\n").</returns>
    public static string? Commit(string edited, string confirmed, bool isDefault, DefaultTextKind kind)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(confirmed);
        var text = BreakTimeRules.NormalizeLineBreaks(edited);
        if (text == confirmed)
        {
            return null;
        }

        if (DefaultTexts.MeansDefault(kind, text))
        {
            return isDefault ? null : string.Empty;
        }

        return text;
    }
}
