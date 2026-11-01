using System.Globalization;
using EagleEye.TrayClient.Overlay;
using EagleEye.TrayClient.UI;
using Moq;
using Xunit;

namespace EagleEye.TrayClient.Tests.UI;

public sealed class TrayMenuTests : IDisposable
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private readonly Mock<IOverlaySettingsStore> _store = new();
    private readonly OverlaySettingsModel _settings;
    private readonly List<ContextMenuStrip> _menus = [];
    private int _aboutClicks;

    public TrayMenuTests()
    {
        _store.Setup(s => s.Load()).Returns(OverlaySettings.Defaults);
        _settings = new OverlaySettingsModel(_store.Object);
    }

    public void Dispose() => _menus.ForEach(m => m.Dispose());

    [Theory]
    [InlineData("de-DE", "Deckkraft|Position|Anzeigen ab|Warnton||App Infos")]
    [InlineData("en-US", "Opacity|Position|Show from|Warning sound||About")]
    public void Structure_TopLevelItems(string culture, string expected)
    {
        var menu = Build(CultureInfo.GetCultureInfo(culture));

        Assert.Equal(expected, string.Join("|", menu.Items.Cast<ToolStripItem>().Select(i => i is ToolStripSeparator ? string.Empty : i.Text)));
        Assert.IsType<ToolWindowContextMenu>(menu);
    }

    [Fact]
    public void Submenus_ToolWindows_ChoicesInMenuOrder_German()
    {
        var menu = Build(German);

        Assert.Equal(["25 %", "50 %", "75 %", "100 %"], Texts(Submenu(menu, 0)));
        Assert.Equal(["Oben links", "Oben rechts", "Unten links", "Unten rechts"], Texts(Submenu(menu, 1)));
        Assert.Equal(["60 Min.", "45 Min.", "30 Min.", "15 Min."], Texts(Submenu(menu, 2)));
        Assert.All(Enumerable.Range(0, 3), i => Assert.IsType<ToolWindowDropDown>(Submenu(menu, i).DropDown));
    }

    [Fact]
    public void Submenus_English()
    {
        var menu = Build(English);

        Assert.Equal(["Top left", "Top right", "Bottom left", "Bottom right"], Texts(Submenu(menu, 1)));
        Assert.Equal(["60 min", "45 min", "30 min", "15 min"], Texts(Submenu(menu, 2)));
    }

    [Fact]
    public void Checks_TheDefaults()
    {
        var menu = Build(English);

        Assert.Equal([false, false, true, false], Checks(Submenu(menu, 0)));
        Assert.Equal([false, true, false, false], Checks(Submenu(menu, 1)));
        Assert.Equal([false, false, true, false], Checks(Submenu(menu, 2)));
        Assert.True(((ToolStripMenuItem)menu.Items[3]).Checked);
    }

    [Fact]
    public void Click_ChangesAndStoresEachSetting()
    {
        var menu = Build(English);

        Submenu(menu, 0).DropDownItems[0].PerformClick();
        Submenu(menu, 1).DropDownItems[3].PerformClick();
        Submenu(menu, 2).DropDownItems[0].PerformClick();
        menu.Items[3].PerformClick();

        Assert.Equal(new OverlaySettings(25, OverlayCorner.BottomRight, 60, false), _settings.Current);
        _store.Verify(s => s.Save(It.IsAny<OverlaySettings>()), Times.Exactly(4));
    }

    [Fact]
    public void Checks_RefreshedWhenTheMenuOrASubmenuOpens()
    {
        var menu = Build(English);
        _settings.Set(new OverlaySettings(50, OverlayCorner.TopLeft, 15, false));

        Invoke(Submenu(menu, 0), "OnDropDownShow", EventArgs.Empty);
        Assert.Equal([false, true, false, false], Checks(Submenu(menu, 0)));

        _settings.Set(new OverlaySettings(50, OverlayCorner.TopLeft, 15, true));
        _settings.Set(new OverlaySettings(50, OverlayCorner.TopLeft, 15, false));
        Invoke(menu, "OnOpening", new System.ComponentModel.CancelEventArgs());
        Assert.Equal([true, false, false, false], Checks(Submenu(menu, 1)));
        Assert.Equal([false, false, false, true], Checks(Submenu(menu, 2)));
        Assert.False(((ToolStripMenuItem)menu.Items[3]).Checked);
    }

    [Fact]
    public void About_CallsTheHandler()
    {
        var menu = Build(English);

        menu.Items[5].PerformClick();

        Assert.Equal(1, _aboutClicks);
    }

    [Fact]
    public void NotifyIcon_HasTheNonPublicShowContextMenu_ForTheLeftButton()
    {
        // TI-10: a .NET update that removes it falls back to menu.Show; this test flags it.
        Assert.NotNull(TrayMenu.ShowContextMenuMethod);
        Assert.Empty(TrayMenu.ShowContextMenuMethod!.GetParameters());
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => TrayMenu.Build(null!, (_, _) => { }, English));
        Assert.Throws<ArgumentNullException>(() => TrayMenu.Build(_settings, null!, English));
        Assert.Throws<ArgumentNullException>(() => TrayMenu.Build(_settings, (_, _) => { }, null!));
        Assert.Throws<ArgumentNullException>(() => TrayMenu.ShowFromLeftClick(null!, new ContextMenuStrip()));
        using var icon = new NotifyIcon();
        Assert.Throws<ArgumentNullException>(() => TrayMenu.ShowFromLeftClick(icon, null!));
    }

    private ContextMenuStrip Build(CultureInfo culture)
    {
        var menu = TrayMenu.Build(_settings, (_, _) => _aboutClicks++, culture);
        _menus.Add(menu);
        return menu;
    }

    private static void Invoke(object target, string method, EventArgs args) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [args.GetType()])!.Invoke(target, [args]);

    private static ToolStripMenuItem Submenu(ContextMenuStrip menu, int index) => (ToolStripMenuItem)menu.Items[index];

    private static List<string> Texts(ToolStripMenuItem submenu) => [.. submenu.DropDownItems.Cast<ToolStripItem>().Select(i => i.Text ?? string.Empty)];

    private static List<bool> Checks(ToolStripMenuItem submenu) => [.. submenu.DropDownItems.Cast<ToolStripMenuItem>().Select(i => i.Checked)];
}
