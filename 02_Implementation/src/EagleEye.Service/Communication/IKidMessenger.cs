using EagleEye.Shared.Models;

namespace EagleEye.Service.Communication;

/// <summary>Shows a message in the kid dialog of a session (ADR-014 §2 as amended by US-006, AC-34, AC-35).</summary>
public interface IKidMessenger
{
    /// <summary>
    /// Asks the newest verified tray connection of the session to show the message; waits at most 3 s. Never throws.
    /// Returns the message state for the log and the history (<see cref="KidMessageStates"/>).
    /// </summary>
    Task<string> ShowAsync(int sessionId, KidMessageDto message, CancellationToken ct = default);
}

/// <summary>Message states of warnings, blocked starts and enforced closes (US-005 AC-28, AC-33; US-006 AC-35, AC-37).</summary>
public static class KidMessageStates
{
    /// <summary>The tray client opened the dialog.</summary>
    public const string Shown = "shown";

    /// <summary>A dialog was open in that tray client; its text was replaced (US-006 AC-34).</summary>
    public const string Replaced = "shown (replaced the open message)";

    /// <summary>A message was already open in a 0.5 tray client and nothing was shown (US-005 AC-32).</summary>
    public const string AlreadyOpen = "not shown (a message is already open)";

    /// <summary>No verified tray client of the session was connected (AC-33).</summary>
    public const string NotConnected = "not shown (tray client not connected)";

    /// <summary>The tray client did not answer within 3 s, or the call failed.</summary>
    public const string NoAnswer = "not shown (tray client did not answer)";

    /// <summary>A more urgent message of the same session was sent in the same tick (US-006 D-4).</summary>
    public const string ReplacedInTick = "not shown (replaced in the same tick)";
}
