namespace EagleEye.ParentApp.Core.Appearance;

/// <summary>
/// The macOS accent colours (US-007 AC-10, ADR-020 §5): maps the global default <c>AppleAccentColor</c> to Apple's
/// system colours (light appearance, sRGB). Fallback of the Mac platform layer when <c>NSColor.controlAccentColor</c>
/// cannot be read. Pure.
/// </summary>
public static class AppleAccentPalette
{
    /// <summary>Graphite (<c>AppleAccentColor</c> = -1).</summary>
    public static RgbColor Graphite { get; } = new(0x8E, 0x8E, 0x93);

    /// <summary>Red (0).</summary>
    public static RgbColor Red { get; } = new(0xFF, 0x3B, 0x30);

    /// <summary>Orange (1).</summary>
    public static RgbColor Orange { get; } = new(0xFF, 0x95, 0x00);

    /// <summary>Yellow (2).</summary>
    public static RgbColor Yellow { get; } = new(0xFF, 0xCC, 0x00);

    /// <summary>Green (3).</summary>
    public static RgbColor Green { get; } = new(0x28, 0xCD, 0x41);

    /// <summary>Blue (4, and the macOS default when the value is absent: blue or multicolour).</summary>
    public static RgbColor Blue { get; } = new(0x00, 0x7A, 0xFF);

    /// <summary>Purple (5).</summary>
    public static RgbColor Purple { get; } = new(0xAF, 0x52, 0xDE);

    /// <summary>Pink (6).</summary>
    public static RgbColor Pink { get; } = new(0xFF, 0x2D, 0x55);

    /// <summary>The accent colour for an <c>AppleAccentColor</c> value; absent = blue; an undefined value = <c>null</c>.</summary>
    /// <param name="value">The global default <c>AppleAccentColor</c>, or <c>null</c> if it is not set.</param>
    public static RgbColor? FromAppleAccentColor(int? value) => value switch
    {
        null => Blue,
        -1 => Graphite,
        0 => Red,
        1 => Orange,
        2 => Yellow,
        3 => Green,
        4 => Blue,
        5 => Purple,
        6 => Pink,
        _ => null,
    };
}
