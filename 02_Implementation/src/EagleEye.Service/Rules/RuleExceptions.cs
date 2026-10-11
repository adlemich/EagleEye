namespace EagleEye.Service.Rules;

/// <summary>The break-time entry does not exist (anymore), e.g. another parent app deleted it (AC-19).</summary>
public sealed class EntryNotFoundException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public EntryNotFoundException()
        : base("The entry no longer exists.")
    {
    }
}

/// <summary>The account already has the maximum number of entries (OQ-8).</summary>
public sealed class TooManyEntriesException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TooManyEntriesException()
        : base("Too many entries.")
    {
    }
}

/// <summary>Which rule a rejected change would break.</summary>
public enum RuleViolation
{
    /// <summary>The end time would not be later than the start time (AC-10).</summary>
    EndNotAfterStart,

    /// <summary>No weekday would stay ticked (AC-11).</summary>
    NoDaySelected,
}

/// <summary>A change was rejected because the stored entry would become invalid (AC-10, AC-11).</summary>
public sealed class RuleValidationException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="violation">The broken rule.</param>
    public RuleValidationException(RuleViolation violation)
        : base($"The change breaks the rule {violation}.")
    {
        Violation = violation;
    }

    /// <summary>The broken rule.</summary>
    public RuleViolation Violation { get; }
}
