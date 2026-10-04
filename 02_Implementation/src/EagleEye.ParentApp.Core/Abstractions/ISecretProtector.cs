namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Encrypts secrets for storage, bound to the current OS user (Windows: DPAPI CurrentUser).</summary>
public interface ISecretProtector
{
    /// <summary>Encrypts the data.</summary>
    byte[] Protect(byte[] data);

    /// <summary>Decrypts data produced by <see cref="Protect"/>.</summary>
    byte[] Unprotect(byte[] data);
}
