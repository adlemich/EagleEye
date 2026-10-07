namespace EagleEye.Service.Diagnostics;

/// <summary>Writes a pairing code to the Windows Event Log (FR-SVC-092, US-002 AC-15).</summary>
public interface IPairingCodeEventLog
{
    /// <summary>Writes the code as event 1000 (Information) to the Application log, source <c>EagleEye</c>.</summary>
    void Write(string code);
}
