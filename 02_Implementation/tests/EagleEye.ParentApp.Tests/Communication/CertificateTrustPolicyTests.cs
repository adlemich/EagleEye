using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Tests.Fakes;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class CertificateTrustPolicyTests
{
    [Fact]
    public void Validate_TrustOnFirstUse_AcceptsAndCapturesThumbprint()
    {
        var policy = CertificateTrustPolicy.TrustOnFirstUse();

        var accepted = policy.Validate(TestSupport.Certificate);

        Assert.True(accepted);
        Assert.Equal(TestSupport.Thumbprint, policy.ObservedThumbprint);
    }

    [Fact]
    public void Validate_PinnedEqual_Accepts()
    {
        var policy = CertificateTrustPolicy.Pinned(TestSupport.Thumbprint);

        Assert.True(policy.Validate(TestSupport.Certificate));
    }

    [Fact]
    public void Validate_PinnedEqualDifferentCase_Accepts()
    {
        var policy = CertificateTrustPolicy.Pinned(TestSupport.Thumbprint.ToLowerInvariant());

        Assert.True(policy.Validate(TestSupport.Certificate));
    }

    [Fact]
    public void Validate_PinnedEqual_NoMismatch()
    {
        var policy = CertificateTrustPolicy.Pinned(TestSupport.Thumbprint);

        policy.Validate(TestSupport.Certificate);

        Assert.False(policy.PinMismatch);
    }

    [Fact]
    public void Validate_PinnedDifferent_RejectsAndRecordsMismatch()
    {
        var policy = CertificateTrustPolicy.Pinned(TestSupport.Thumbprint);

        var accepted = policy.Validate(TestSupport.OtherCertificate);

        Assert.False(accepted);
        Assert.True(policy.PinMismatch);
    }

    [Fact]
    public void Validate_NullCertificate_Rejects()
    {
        var policy = CertificateTrustPolicy.TrustOnFirstUse();

        Assert.False(policy.Validate(null));
        Assert.Null(policy.ObservedThumbprint);
    }

    [Fact]
    public void TrustOnFirstUse_HasNoPin()
    {
        Assert.Null(CertificateTrustPolicy.TrustOnFirstUse().PinnedThumbprint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Pinned_MissingThumbprint_ThrowsArgumentException(string? thumbprint)
    {
        Assert.ThrowsAny<ArgumentException>(() => CertificateTrustPolicy.Pinned(thumbprint!));
    }
}
