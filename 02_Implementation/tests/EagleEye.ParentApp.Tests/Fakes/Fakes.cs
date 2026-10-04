using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Tests.Fakes;

/// <summary>Runs posted actions immediately on the calling thread.</summary>
internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>In-memory secret store.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = new();

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Values.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>Reversible, visibly different "protection" (XOR) for tests.</summary>
internal sealed class XorProtector : ISecretProtector
{
    private const byte Key = 0x5A;

    public byte[] Protect(byte[] data) => data.Select(b => (byte)(b ^ Key)).ToArray();

    public byte[] Unprotect(byte[] data) => Protect(data);
}

/// <summary>Test helpers shared by several test classes.</summary>
internal static class TestSupport
{
    private static readonly Lazy<X509Certificate2> LazyCertificate = new(() =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=EagleEye", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100));
    });

    private static readonly Lazy<X509Certificate2> LazyOtherCertificate = new(() =>
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Other", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(100));
    });

    /// <summary>A self-signed test certificate ("the service").</summary>
    public static X509Certificate2 Certificate => LazyCertificate.Value;

    /// <summary>A different certificate ("an impostor").</summary>
    public static X509Certificate2 OtherCertificate => LazyOtherCertificate.Value;

    /// <summary>SHA-256 thumbprint of <see cref="Certificate"/>.</summary>
    public static string Thumbprint => Certificate.GetCertHashString(HashAlgorithmName.SHA256);

    /// <summary>Runs <paramref name="action"/> with the given UI culture (and culture).</summary>
    public static T InCulture<T>(string culture, Func<T> action)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = original;
        }
    }
}
