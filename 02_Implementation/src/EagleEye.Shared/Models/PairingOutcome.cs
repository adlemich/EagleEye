namespace EagleEye.Shared.Models;

/// <summary>
/// Result of a pairing code submission (<c>IParentHub.SubmitPairingCode</c>).
/// Every outcome except <see cref="Success"/> invalidates the pending code.
/// </summary>
public enum PairingOutcome
{
    /// <summary>The device is paired; device ID and token are returned.</summary>
    Success,

    /// <summary>The code does not match the pending code.</summary>
    WrongCode,

    /// <summary>The pending code is older than the code lifetime.</summary>
    CodeExpired,

    /// <summary>There is no pending code for this connection.</summary>
    NoPendingCode,

    /// <summary>The device name is empty, too long or contains control characters.</summary>
    InvalidDeviceName,

    /// <summary>The code is not exactly 6 digits.</summary>
    InvalidCodeFormat,
}
