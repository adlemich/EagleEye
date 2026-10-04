namespace EagleEye.ParentApp.Core.Communication;

/// <summary>Pairing status shown in the "Server connection" section (US-002 AC-8).</summary>
public enum PairingStatus
{
    /// <summary>Not paired.</summary>
    NotPaired,

    /// <summary>Pairing in progress.</summary>
    InProgress,

    /// <summary>Paired.</summary>
    Paired,
}
