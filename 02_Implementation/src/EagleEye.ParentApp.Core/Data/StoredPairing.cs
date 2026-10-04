namespace EagleEye.ParentApp.Core.Data;

/// <summary>The app's pairing with a service (US-002 allows one).</summary>
/// <param name="Host">The host as entered by the parent.</param>
/// <param name="DeviceId">The device ID issued by the service.</param>
/// <param name="DeviceName">The device name given at pairing.</param>
/// <param name="CertificateThumbprint">Pinned SHA-256 thumbprint of the service certificate (hex).</param>
/// <param name="PairedAtUtc">When the pairing was made.</param>
/// <param name="Token">The device token; a secret, stored via <c>ISecretStore</c>, never logged or shown.</param>
public sealed record StoredPairing(
    string Host,
    string DeviceId,
    string DeviceName,
    string CertificateThumbprint,
    DateTimeOffset PairedAtUtc,
    string Token)
{
    /// <summary>Omits the token, so that it never ends up in logs or diagnostics.</summary>
    public override string ToString() => $"StoredPairing {{ Host = {Host}, DeviceId = {DeviceId}, DeviceName = {DeviceName} }}";
}
