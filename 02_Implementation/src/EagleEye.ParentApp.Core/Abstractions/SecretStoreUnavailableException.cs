namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>
/// The platform secret store refused or failed the access (ADR-020 §2), e.g. the macOS Keychain
/// prompt was denied. Distinct from "no secret stored", which <see cref="ISecretStore.GetAsync"/>
/// reports as <c>null</c>. The message carries the platform status, never the secret.
/// </summary>
public sealed class SecretStoreUnavailableException : InvalidOperationException
{
    /// <summary>Creates the exception for a failed secret-store operation.</summary>
    /// <param name="operation">The operation that failed, e.g. <c>read</c>.</param>
    /// <param name="status">The platform status code (e.g. the macOS <c>OSStatus</c>).</param>
    public SecretStoreUnavailableException(string operation, int status)
        : base($"The secret store refused the {operation} operation (status {status}).")
    {
        Operation = operation;
        Status = status;
    }

    /// <summary>The operation that failed.</summary>
    public string Operation { get; }

    /// <summary>The platform status code.</summary>
    public int Status { get; }
}
