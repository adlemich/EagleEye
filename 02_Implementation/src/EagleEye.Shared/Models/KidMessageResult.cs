namespace EagleEye.Shared.Models;

/// <summary>The tray client's answer to <c>ITrayClientCallback.ShowKidMessage</c> (ADR-014 §2 as amended by US-006).</summary>
public enum KidMessageResult
{
    /// <summary>The dialog was created and is visible.</summary>
    Shown,

    /// <summary>A message was already open and nothing new was shown. Kept for wire stability; no longer sent from 0.6.</summary>
    AlreadyOpen,

    /// <summary>A dialog was open; its text was replaced by the new message (US-006 AC-34).</summary>
    Replaced,
}
