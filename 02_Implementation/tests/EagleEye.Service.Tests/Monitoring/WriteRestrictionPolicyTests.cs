using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class WriteRestrictionPolicyTests
{
    private readonly WriteRestrictionPolicy _policy = new();

    [Fact]
    public void New_UsesWriteRestriction()
    {
        Assert.True(_policy.UseWriteRestriction);
    }

    [Fact]
    public void ReportEarlyExit_Once_KeepsRestriction()
    {
        Assert.Equal((false, true), (_policy.ReportEarlyExit(), _policy.UseWriteRestriction));
    }

    [Fact]
    public void ReportEarlyExit_Twice_SwitchesToFallbackOnce()
    {
        _policy.ReportEarlyExit();

        Assert.Equal((true, false), (_policy.ReportEarlyExit(), _policy.UseWriteRestriction));
        Assert.False(_policy.ReportEarlyExit());
    }

    [Fact]
    public void ReportEarlyExit_AfterAgentWorked_NeverFallsBack()
    {
        _policy.ReportWorking();

        _policy.ReportEarlyExit();
        _policy.ReportEarlyExit();

        Assert.True(_policy.UseWriteRestriction);
    }
}
