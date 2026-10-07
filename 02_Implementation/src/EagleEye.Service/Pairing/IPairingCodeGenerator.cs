namespace EagleEye.Service.Pairing;

/// <summary>Generates pairing codes.</summary>
public interface IPairingCodeGenerator
{
    /// <summary>Returns a new random 6-digit code (leading zeros kept).</summary>
    string Generate();
}
