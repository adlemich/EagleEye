namespace EagleEye.Service.Communication;

/// <summary>Shows the display text in the tray client of a session (ADR-014 §2, AC-30 to AC-33).</summary>
public interface IKidMessenger
{
    /// <summary>
    /// Asks the newest verified tray connection of the session to show the text; waits at most 3 s. Never throws.
    /// Returns the message state for the log and the history (<see cref="KidMessageStates"/>).
    /// </summary>
    Task<string> ShowAsync(int sessionId, string displayText, CancellationToken ct = default);
}

/// <summary>Message states of a blocked start (AC-28, AC-33, plan D-10).</summary>
public static class KidMessageStates
{
    /// <summary>The tray client opened the dialog.</summary>
    public const string Shown = "shown";

    /// <summary>A break-time message was already open in that tray client (AC-32).</summary>
    public const string AlreadyOpen = "not shown (a message is already open)";

    /// <summary>No verified tray client of the session was connected (AC-33).</summary>
    public const string NotConnected = "not shown (tray client not connected)";

    /// <summary>The tray client did not answer within 3 s, or the call failed.</summary>
    public const string NoAnswer = "not shown (tray client did not answer)";
}
