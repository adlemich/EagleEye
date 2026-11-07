using System.Globalization;
using System.Reflection;
using EagleEye.TrayClient.Overlay;

namespace EagleEye.TrayClient.UI;

/// <summary>
/// The tray menu (US-010 AC-15, Decision 10): "Opacity", "Position", "Show from" submenus, the "Warning sound" check item,
/// a separator and "About" (unchanged). All popups are tool windows (Decision 9). The check marks are refreshed when the
/// menu or a submenu opens (spike S-18); a click changes and stores the setting at once.
/// </summary>
internal static class TrayMenu
{
    /// <summary>The non-public method WinForms uses to open the context menu on the right button (TI-10).</summary>
    internal static readonly MethodInfo? ShowContextMenuMethod =
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>Builds the menu for <paramref name="settings"/> in <paramref name="culture"/>.</summary>
    public static ContextMenuStrip Build(OverlaySettingsModel settings, EventHandler onAbout, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(onAbout);
        ArgumentNullException.ThrowIfNull(culture);
        var checks = new List<(ToolStripMenuItem Item, Func<bool> IsChecked)>();
        void Refresh() => checks.ForEach(c => c.Item.Checked = c.IsChecked());

        ToolStripMenuItem Choice(string text, Func<OverlaySettings, bool> isChosen, Func<OverlaySettings, OverlaySettings> apply)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => settings.Set(apply(settings.Current));
            checks.Add((item, () => isChosen(settings.Current)));
            return item;
        }

        ToolStripMenuItem Submenu(string key, IEnumerable<ToolStripMenuItem> items)
        {
            var submenu = new ToolStripMenuItem(TrayTexts.Get(key, culture)) { DropDown = new ToolWindowDropDown() };
            submenu.DropDownItems.AddRange([.. items]);
            submenu.DropDownOpening += (_, _) => Refresh();
            return submenu;
        }

        var opacity = Submenu("MenuOpacity", OverlaySettings.Opacities.Select(p => Choice(
            string.Format(culture, TrayTexts.Get("OpacityFormat", culture), p), s => s.OpacityPercent == p, s => s with { OpacityPercent = p })));
        var position = Submenu("MenuPosition", Enum.GetValues<OverlayCorner>().Select(c => Choice(
            TrayTexts.Get($"Corner{c}", culture), s => s.Corner == c, s => s with { Corner = c })));
        var showFrom = Submenu("MenuShowFrom", OverlaySettings.ShowFromChoices.Select(m => Choice(
            string.Format(culture, TrayTexts.Get("ShowFromFormat", culture), m), s => s.ShowFromMinutes == m, s => s with { ShowFromMinutes = m })));
        var sound = Choice(TrayTexts.Get("MenuWarningSound", culture), s => s.Sound, s => s with { Sound = !s.Sound });

        var menu = new ToolWindowContextMenu();
        menu.Items.AddRange([opacity, position, showFrom, sound, new ToolStripSeparator()]);
        menu.Items.Add(TrayTexts.Get("AboutMenuItem", culture), null, onAbout);
        menu.Opening += (_, _) => Refresh();
        Refresh();
        return menu;
    }

    /// <summary>Opens the menu as the right button does (left button, AC-15); falls back to showing it at the cursor.</summary>
    public static void ShowFromLeftClick(NotifyIcon notifyIcon, ContextMenuStrip menu)
    {
        ArgumentNullException.ThrowIfNull(notifyIcon);
        ArgumentNullException.ThrowIfNull(menu);
        if (ShowContextMenuMethod is { } method)
        {
            method.Invoke(notifyIcon, null);
            return;
        }

        menu.Show(Cursor.Position);
    }
}
