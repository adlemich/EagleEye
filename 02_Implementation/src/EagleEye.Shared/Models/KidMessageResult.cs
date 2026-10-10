namespace EagleEye.Shared.Models;

/// <summary>The tray client's answer to <c>ITrayClientCallback.ShowBreakTimeMessage</c> (ADR-014 §2).</summary>
public enum KidMessageResult
{
    /// <summary>The dialog was created and is visible.</summary>
    Shown,

    /// <summary>A break-time message is already open in this tray client; nothing new is shown (AC-32).</summary>
    AlreadyOpen,
}
