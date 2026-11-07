namespace EagleEye.TrayClient.Overlay;

/// <summary>The screen corner of the overlay (US-010 AC-15); the values are the stored registry values.</summary>
internal enum OverlayCorner
{
    /// <summary>Top left.</summary>
    TopLeft = 0,

    /// <summary>Top right (default).</summary>
    TopRight = 1,

    /// <summary>Bottom left.</summary>
    BottomLeft = 2,

    /// <summary>Bottom right.</summary>
    BottomRight = 3,
}

/// <summary>The overlay settings of the signed-in Windows account (FR-TRAY-031, US-010 AC-15 to AC-18, Decision 10).</summary>
/// <param name="OpacityPercent">Background opacity: 25, 50, 75 or 100.</param>
/// <param name="Corner">The screen corner.</param>
/// <param name="ShowFromMinutes">The display threshold: 60, 45, 30 or 15.</param>
/// <param name="Sound">Whether the warning sound plays.</param>
internal sealed record OverlaySettings(int OpacityPercent, OverlayCorner Corner, int ShowFromMinutes, bool Sound)
{
    /// <summary>The opacity choices of the menu.</summary>
    public static IReadOnlyList<int> Opacities { get; } = [25, 50, 75, 100];

    /// <summary>The display threshold choices of the menu, in menu order.</summary>
    public static IReadOnlyList<int> ShowFromChoices { get; } = [60, 45, 30, 15];

    /// <summary>The defaults: 75 %, top right, 30 minutes, sound on (AC-16).</summary>
    public static OverlaySettings Defaults { get; } = new(75, OverlayCorner.TopRight, 30, true);

    /// <summary>
    /// Settings from stored values: each value outside the menu's choices (or missing) falls back to its default, so editing
    /// the registry can never hide the overlay (OQ-9).
    /// </summary>
    public static OverlaySettings Sanitize(int? opacity, int? corner, int? showFrom, int? sound)
    {
        return new OverlaySettings(
            opacity is { } o && Opacities.Contains(o) ? o : Defaults.OpacityPercent,
            corner is { } c && Enum.IsDefined((OverlayCorner)c) ? (OverlayCorner)c : Defaults.Corner,
            showFrom is { } s && ShowFromChoices.Contains(s) ? s : Defaults.ShowFromMinutes,
            sound is 0 or 1 ? sound == 1 : Defaults.Sound);
    }
}

/// <summary>Where the overlay settings are kept (US-010 Decision 10).</summary>
internal interface IOverlaySettingsStore
{
    /// <summary>The stored settings, sanitized; the defaults when nothing is stored or the store cannot be read.</summary>
    OverlaySettings Load();

    /// <summary>Stores the settings; a failing write is ignored (the value stays in memory for this run).</summary>
    void Save(OverlaySettings settings);
}

/// <summary>The current overlay settings: loaded once at start, saved on each change (US-010 Decision 10). UI thread only.</summary>
internal sealed class OverlaySettingsModel
{
    private readonly IOverlaySettingsStore _store;

    /// <summary>Loads the settings from <paramref name="store"/>.</summary>
    public OverlaySettingsModel(IOverlaySettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Current = store.Load();
    }

    /// <summary>Raised after a change.</summary>
    public event Action? Changed;

    /// <summary>The current settings.</summary>
    public OverlaySettings Current { get; private set; }

    /// <summary>Applies and stores <paramref name="settings"/>; nothing happens when they are unchanged.</summary>
    public void Set(OverlaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings == Current)
        {
            return;
        }

        Current = settings;
        _store.Save(settings);
        Changed?.Invoke();
    }
}
