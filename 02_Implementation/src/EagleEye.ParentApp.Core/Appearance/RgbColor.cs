namespace EagleEye.ParentApp.Core.Appearance;

/// <summary>An opaque colour (8 bits per channel), independent of MAUI and the platform.</summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>White.</summary>
    public static RgbColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>Black.</summary>
    public static RgbColor Black { get; } = new(0, 0, 0);
}
