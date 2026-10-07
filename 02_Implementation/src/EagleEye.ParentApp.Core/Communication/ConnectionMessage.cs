namespace EagleEye.ParentApp.Core.Communication;

/// <summary>The last error or information of the coordinator; localized by the view models.</summary>
public enum ConnectionMessage
{
    /// <summary>No message.</summary>
    None,

    /// <summary>The entered host is not a valid hostname or IP address.</summary>
    InvalidHost,

    /// <summary>The service at the host could not be reached (US-002 AC-12).</summary>
    Unreachable,

    /// <summary>The code is not 6 digits.</summary>
    CodeFormat,

    /// <summary>The code was wrong (AC-18).</summary>
    WrongCode,

    /// <summary>The code has expired (AC-19).</summary>
    CodeExpired,

    /// <summary>The service has no valid code for this connection.</summary>
    NoPendingCode,

    /// <summary>The device name is empty or too long (AC-17).</summary>
    DeviceNameRequired,

    /// <summary>The service presented a certificate different from the pinned one (ADR-008 §3).</summary>
    CertificateChanged,

    /// <summary>The service no longer knows this app's pairing; it was deleted locally.</summary>
    PairingLost,

    /// <summary>Removing the pairing failed; the pairing is kept.</summary>
    RemoveFailed,
}
