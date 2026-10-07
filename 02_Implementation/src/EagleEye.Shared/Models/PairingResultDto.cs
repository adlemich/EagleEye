namespace EagleEye.Shared.Models;

/// <summary>
/// Result of <c>IParentHub.SubmitPairingCode</c>. On success, <see cref="DeviceId"/> and
/// <see cref="Token"/> are set (token: 256-bit, Base64Url). The token is a secret: it is never logged.
/// </summary>
/// <param name="Outcome">The outcome of the submission.</param>
/// <param name="DeviceId">The new device ID, on success.</param>
/// <param name="Token">The authentication token, on success.</param>
public sealed record PairingResultDto(PairingOutcome Outcome, string? DeviceId, string? Token);
