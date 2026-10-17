using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class ParentHubGatewayAppRulesTests
{
    private static readonly AppRulesDto Apps = new(2, null, "S-1-5-21-1", [], null);
    private static readonly WarningSettingsDto Warnings = new(3, null, 5, null, 1, null);

    [Fact]
    public void AppRulesAndWarningSettings_ForwardedOnlyFromCurrentClientWhileConnected()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var first = new Mock<IParentHubClient>();
        var second = new Mock<IParentHubClient>();
        var apps = new List<AppRulesDto>();
        var warnings = new List<WarningSettingsDto>();
        gateway.AppRulesChanged += apps.Add;
        gateway.WarningSettingsChanged += warnings.Add;

        gateway.SetConnected(first.Object);
        first.Raise(c => c.AppRulesChanged += null, Apps);
        first.Raise(c => c.WarningSettingsChanged += null, Warnings);
        gateway.SetDisconnected();
        first.Raise(c => c.AppRulesChanged += null, Apps);
        gateway.SetConnected(second.Object);
        first.Raise(c => c.WarningSettingsChanged += null, Warnings);
        second.Raise(c => c.AppRulesChanged += null, Apps);
        second.Raise(c => c.WarningSettingsChanged += null, Warnings);

        Assert.Equal((2, 2), (apps.Count, warnings.Count));
        first.VerifyRemove(c => c.AppRulesChanged -= It.IsAny<Action<AppRulesDto>>(), Times.Once);
        first.VerifyRemove(c => c.WarningSettingsChanged -= It.IsAny<Action<WarningSettingsDto>>(), Times.Once);
    }

    [Fact]
    public void Broadcasts_WithoutHandlers_DoNotThrow()
    {
        var gateway = new ParentHubGateway(new FakeTimeProvider());
        var client = new Mock<IParentHubClient>();
        gateway.SetConnected(client.Object);

        client.Raise(c => c.AppRulesChanged += null, Apps);
        client.Raise(c => c.WarningSettingsChanged += null, Warnings);

        Assert.True(gateway.IsConnected);
    }
}
