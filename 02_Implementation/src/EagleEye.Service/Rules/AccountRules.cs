using System.Collections.Immutable;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Rules;

/// <summary>The rules of one account: its break-time entries (creation order) and its display text.</summary>
/// <param name="Entries">The entries, in creation order.</param>
/// <param name="CustomText">The parent's display text, or <c>null</c> for the default text (AC-15).</param>
public sealed record AccountRules(ImmutableList<BreakTimeEntryDto> Entries, string? CustomText)
{
    /// <summary>An account without entries and with the default text.</summary>
    public static AccountRules Empty { get; } = new([], null);

    /// <summary>The text shown to the kid.</summary>
    public string DisplayText => CustomText ?? BreakTimeRules.DefaultDisplayText;

    /// <summary>Whether the default text applies.</summary>
    public bool IsDefaultText => CustomText is null;

    /// <summary>Whether at least one entry is switched on (accounts without one skip the gate, AC-29).</summary>
    public bool HasActiveEntry => Entries.Any(e => e.IsActive);

    /// <summary>Whether nothing is stored for the account.</summary>
    public bool IsEmpty => Entries.IsEmpty && CustomText is null;

    /// <summary>The entry with the id, or <c>null</c>.</summary>
    public BreakTimeEntryDto? Find(long entryId) => Entries.Find(e => e.EntryId == entryId);
}
