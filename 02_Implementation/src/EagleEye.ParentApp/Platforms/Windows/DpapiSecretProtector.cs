using System.Security.Cryptography;
using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Platforms.Windows;

/// <summary>
/// DPAPI with <see cref="DataProtectionScope.CurrentUser"/>: only the Windows user who paired the
/// app can decrypt the token (ADR-008 §5, ADR-009). Verified manually.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    /// <inheritdoc />
    public byte[] Protect(byte[] data) => ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);

    /// <inheritdoc />
    public byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
