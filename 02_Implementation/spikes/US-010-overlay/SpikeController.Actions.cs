using System.Diagnostics;

namespace OverlaySpike;

/// <summary>Hotkeys and tray menu (the menu is for use between test cases only: opening it takes the focus).</summary>
internal sealed partial class SpikeController
{
    private const string HotkeyPrefix = "Strg+Alt+Umschalt+";

    private static readonly (int Id, Keys Key, string Name)[] HotkeyTable =
    [
        (1, Keys.D0, "0 Neustart"),
        (2, Keys.D5, "5 Sprung 5:10"),
        (3, Keys.D1, "1 Sprung 1:10"),
        (4, Keys.O, "O Overlay"),
        (5, Keys.R, "R Re-assert"),
        (6, Keys.B, "B Ton"),
        (7, Keys.C, "C Ecke"),
        (8, Keys.M, "M Markierung"),
    ];

    private static string CornerName(Corner c) => c switch
    {
        Corner.TopRight => "Oben rechts",
        Corner.BottomRight => "Unten rechts",
        Corner.BottomLeft => "Unten links",
        _ => "Oben links",
    };

    private static string OnOff(bool value) => value ? "AN" : "AUS";

    private void RegisterHotkeys()
    {
        foreach (var (id, key, name) in HotkeyTable)
        {
            var error = hotkeys.Register(id, key);
            if (error != 0)
            {
                failedHotkeys.Add(name[..1]);
                log.Write("ERROR", $"hotkey {HotkeyPrefix}{name[..1]} not registered (Win32 error {error}, taken by another program?)");
            }
        }

        log.Write("START", failedHotkeys.Count == 0 ? "all 8 hotkeys registered" : $"hotkeys NOT registered: {string.Join(",", failedHotkeys)}");
    }

    private void OnHotkey(int id)
    {
        switch (id)
        {
            case 1: Restart("KEY"); break;
            case 2: Jump("KEY", TimeSpan.FromSeconds(310)); break;
            case 3: Jump("KEY", TimeSpan.FromSeconds(70)); break;
            case 4: ToggleOverlay("KEY"); break;
            case 5: ToggleReassert("KEY"); break;
            case 6: ToggleBeep("KEY"); break;
            case 7: SetCorner("KEY", (Corner)(((int)settings.Corner + 1) % 4)); break;
            case 8: Mark("KEY"); break;
            default: log.Write("KEY", $"unknown hotkey id {id}"); break;
        }
    }

    private void Feedback(string source, string action, string text)
    {
        log.Write(source, $"{action} -> {text}");
        feedback = text;
        feedbackUntil = runClock.Elapsed + FeedbackDuration;
        forceRender = true;
        Render();
        timer.Interval = NextInterval();
    }

    private void Restart(string source)
    {
        SetCountdown(TimeSpan.FromMinutes(settings.StartMinutes));
        Feedback(source, "restart", $"Neustart {settings.StartMinutes}:00");
    }

    private void Jump(string source, TimeSpan to)
    {
        SetCountdown(to);
        Feedback(source, "jump", $"Sprung {FormatTime((int)to.TotalSeconds)}");
    }

    private void ToggleOverlay(string source)
    {
        hidden = !hidden;
        if (hidden)
        {
            log.Write(source, "overlay -> AUS");
            overlay.HideOverlay();
            return;
        }

        overlay.ShowNoActivate();
        overlay.Reassert();
        Feedback(source, "overlay", "Overlay: AN");
    }

    private void ToggleReassert(string source)
    {
        settings.Reassert = !settings.Reassert;
        Feedback(source, "reassert", $"Re-assert: {OnOff(settings.Reassert)}");
    }

    private void ToggleBeep(string source)
    {
        settings.Beep = !settings.Beep;
        Feedback(source, "beep", $"Ton: {OnOff(settings.Beep)}");
    }

    private void SetCorner(string source, Corner corner)
    {
        settings.Corner = corner;
        Feedback(source, "corner", $"Ecke: {CornerName(corner)}");
    }

    private void SetOpacity(string source, int opacity)
    {
        settings.Opacity = opacity;
        Feedback(source, "opacity", $"Deckkraft: {opacity} %");
    }

    private void SetAlpha(string source, bool uniform)
    {
        if (settings.Uniform == uniform)
        {
            return;
        }

        settings.Uniform = uniform;
        var old = overlay;
        overlay = CreateOverlay();
        old.Close();
        old.Dispose();
        if (hidden)
        {
            overlay.HideOverlay();
        }

        overlay.Reassert();
        Feedback(source, "alpha", uniform ? "Alpha: einheitlich" : "Alpha: pro Pixel");
    }

    private void Mark(string source)
    {
        markCount++;
        log.Write("MARK", $"MARK #{markCount} (via {source})");
        Feedback(source, "mark", $"MARK {markCount}");
    }

    private NotifyIcon CreateTray()
    {
        var icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = TrayText(),
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };
        return icon;
    }

    private string TrayText()
    {
        var text = "OverlaySpike – " + HotkeyPrefix + "…";
        if (failedHotkeys.Count > 0)
        {
            text += $" (belegt: {string.Join(",", failedHotkeys)})";
        }

        text += " | Log: " + log.Folder;
        return text.Length > 127 ? text[..126] + "…" : text;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ToolWindowContextMenu();
        menu.Items.Add(Item("Countdown neu starten", "0", () => Restart("MENU")));
        menu.Items.Add(Item("Springe zu 5:00 (ab 5:10)", "5", () => Jump("MENU", TimeSpan.FromSeconds(310))));
        menu.Items.Add(Item("Springe zu 1:00 (ab 1:10)", "1", () => Jump("MENU", TimeSpan.FromSeconds(70))));
        var overlayItem = Item("Overlay ein/aus", "O", () => ToggleOverlay("MENU"));
        var reassertItem = Item("Top-most erneuern an/aus", "R", () => ToggleReassert("MENU"));
        var beepItem = Item("Ton an/aus", "B", () => ToggleBeep("MENU"));
        menu.Items.AddRange([overlayItem, reassertItem, beepItem, new ToolStripSeparator()]);

        var corner = new ToolStripMenuItem("Ecke") { ShortcutKeyDisplayString = HotkeyPrefix + "C (nächste)", DropDown = new ToolWindowDropDown() };
        foreach (var c in Enum.GetValues<Corner>())
        {
            corner.DropDownItems.Add(Item(CornerName(c), null, () => SetCorner("MENU", c), () => settings.Corner == c));
        }

        var opacity = new ToolStripMenuItem("Deckkraft") { DropDown = new ToolWindowDropDown() };
        foreach (var p in new[] { 30, 50, 70 })
        {
            opacity.DropDownItems.Add(Item($"{p} %", null, () => SetOpacity("MENU", p), () => settings.Opacity == p));
        }

        var alpha = new ToolStripMenuItem("Alpha") { DropDown = new ToolWindowDropDown() };
        alpha.DropDownItems.Add(Item("pro Pixel", null, () => SetAlpha("MENU", false), () => !settings.Uniform));
        alpha.DropDownItems.Add(Item("einheitlich", null, () => SetAlpha("MENU", true), () => settings.Uniform));
        menu.Items.AddRange([corner, opacity, alpha, new ToolStripSeparator()]);
        menu.Items.Add(Item("Markierung ins Log", "M", () => Mark("MENU")));
        menu.Items.Add(Item("Log-Ordner öffnen", null, OpenLogFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Beenden", null, () => Shutdown("END", Summary())));
        checkedItems.Add((overlayItem, () => !hidden));
        checkedItems.Add((reassertItem, () => settings.Reassert));
        checkedItems.Add((beepItem, () => settings.Beep));
        menu.Opening += (_, _) => RefreshChecks();
        corner.DropDownOpening += (_, _) => RefreshChecks();
        opacity.DropDownOpening += (_, _) => RefreshChecks();
        alpha.DropDownOpening += (_, _) => RefreshChecks();
        return menu;
    }

    private readonly List<(ToolStripMenuItem Item, Func<bool> IsChecked)> checkedItems = [];

    private void RefreshChecks()
    {
        foreach (var (item, isChecked) in checkedItems)
        {
            item.Checked = isChecked();
        }
    }

    private ToolStripMenuItem Item(string text, string? hotkey, Action action, Func<bool>? isChecked = null)
    {
        var item = new ToolStripMenuItem(text);
        if (hotkey is not null)
        {
            item.ShortcutKeyDisplayString = HotkeyPrefix + hotkey;
        }

        item.Click += (_, _) => action();
        if (isChecked is not null)
        {
            checkedItems.Add((item, isChecked));
            item.Checked = isChecked();
        }

        return item;
    }

    private void OpenLogFolder()
    {
        log.Write("MENU", $"open log folder {log.Folder}");
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{log.Folder}\"") { UseShellExecute = false });
    }
}
