using EagleEye.TrayClient.UI;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

/// <summary>US-010 Decision 9 (FR-TRAY-032, spike D-10): the tray's own popups are tool windows, never app windows.</summary>
public sealed class TrayWindowStylesTests
{
    [Theory]
    [InlineData(TrayWindowStyles.MenuExStyle)]
    [InlineData(TrayWindowStyles.OverlayExStyle)]
    public void Masks_ToolWindow_NeverAppWindow(int mask)
    {
        Assert.Equal(TrayWindowStyles.ToolWindow, mask & TrayWindowStyles.ToolWindow);
        Assert.Equal(0, mask & TrayWindowStyles.AppWindow);
    }

    [Fact]
    public void Overlay_LayeredTransparentTopmostNoActivate()
    {
        // The spike measured the overlay's exstyle 0x080900A8 = these bits plus WS_EX_WINDOWEDGE/CONTROLPARENT of WinForms.
        Assert.Equal(0x080800A8, TrayWindowStyles.OverlayExStyle);
        Assert.Equal(0x080800A8, 0x080900A8 & TrayWindowStyles.OverlayExStyle);
    }

    [Fact]
    public void Menu_ToolWindowOnly()
    {
        Assert.Equal(0x80, TrayWindowStyles.MenuExStyle);
    }
}
