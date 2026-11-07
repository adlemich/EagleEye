using EagleEye.TrayClient.Overlay;
using Moq;
using Xunit;

namespace EagleEye.TrayClient.Tests.Overlay;

public sealed class OverlaySettingsTests
{
    [Fact]
    public void Defaults_75TopRight30SoundOn()
    {
        Assert.Equal(new OverlaySettings(75, OverlayCorner.TopRight, 30, true), OverlaySettings.Defaults);
        Assert.Equal([25, 50, 75, 100], OverlaySettings.Opacities);
        Assert.Equal([60, 45, 30, 15], OverlaySettings.ShowFromChoices);
    }

    [Fact]
    public void Sanitize_ValidValues_Kept()
    {
        Assert.Equal(new OverlaySettings(25, OverlayCorner.BottomLeft, 60, false), OverlaySettings.Sanitize(25, 2, 60, 0));
        Assert.Equal(new OverlaySettings(100, OverlayCorner.TopLeft, 15, true), OverlaySettings.Sanitize(100, 0, 15, 1));
        Assert.Equal(OverlayCorner.BottomRight, OverlaySettings.Sanitize(null, 3, null, null).Corner);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData(0, -1, 0, 2)]
    [InlineData(30, 4, 20, -1)]
    [InlineData(101, 99, 61, 5)]
    public void Sanitize_InvalidOrMissing_EachFallsBackToItsDefault(int? opacity, int? corner, int? showFrom, int? sound)
    {
        Assert.Equal(OverlaySettings.Defaults, OverlaySettings.Sanitize(opacity, corner, showFrom, sound));
    }

    [Fact]
    public void Model_LoadsOnce_SetStoresAndRaises_UnchangedIgnored()
    {
        var store = new Mock<IOverlaySettingsStore>();
        store.Setup(s => s.Load()).Returns(OverlaySettings.Defaults);
        var model = new OverlaySettingsModel(store.Object);
        var raised = 0;
        model.Changed += () => raised++;
        var changed = OverlaySettings.Defaults with { Corner = OverlayCorner.BottomLeft };

        model.Set(changed);
        model.Set(changed);

        Assert.Equal((changed, 1), (model.Current, raised));
        store.Verify(s => s.Save(changed), Times.Once);
        store.Verify(s => s.Load(), Times.Once);
    }

    [Fact]
    public void Model_Guards()
    {
        var store = new Mock<IOverlaySettingsStore>();
        store.Setup(s => s.Load()).Returns(OverlaySettings.Defaults);

        Assert.Throws<ArgumentNullException>(() => new OverlaySettingsModel(null!));
        Assert.Throws<ArgumentNullException>(() => new OverlaySettingsModel(store.Object).Set(null!));
    }
}
