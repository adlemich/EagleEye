namespace EagleEye.Shared.Models;

/// <summary>The kid texts that have a language-following default (ADR-016).</summary>
public enum DefaultTextKind
{
    /// <summary>The break display text (US-005 FR-APP-052).</summary>
    BreakDisplayText,

    /// <summary>The budget text of an account (FR-APP-053).</summary>
    BudgetText,
}
