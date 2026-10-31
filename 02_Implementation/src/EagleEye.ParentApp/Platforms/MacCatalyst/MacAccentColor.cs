using System.Runtime.InteropServices;
using EagleEye.ParentApp.Core.Appearance;
using Foundation;
using ObjCRuntime;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// Reads the macOS accent colour (ADR-020 §5, US-007 AC-10): AppKit <c>NSColor.controlAccentColor</c> through the
/// Objective-C runtime (AppKit is loaded in a Catalyst process), converted to sRGB; if that is not available, the
/// global default <c>AppleAccentColor</c> mapped by <see cref="AppleAccentPalette"/>. Not unit-tested (platform code).
/// </summary>
public sealed class MacAccentColor
{
    private const string ObjCLibrary = "/usr/lib/libobjc.dylib";
    private const string AccentColorKey = "AppleAccentColor";

    /// <summary>The current accent colour, or <c>null</c> if none can be determined.</summary>
    public RgbColor? Read() => ReadControlAccentColor() ?? ReadAccentDefault();

    private static RgbColor? ReadControlAccentColor()
    {
        var colorClass = Class.GetHandle("NSColor");
        var colorSpaceClass = Class.GetHandle("NSColorSpace");
        if (colorClass == IntPtr.Zero || colorSpaceClass == IntPtr.Zero)
        {
            return null;
        }

        var accent = SendIntPtr(colorClass, Selector.GetHandle("controlAccentColor"));
        var srgb = SendIntPtr(colorSpaceClass, Selector.GetHandle("sRGBColorSpace"));
        var converted = accent == IntPtr.Zero || srgb == IntPtr.Zero
            ? IntPtr.Zero
            : SendIntPtr(accent, Selector.GetHandle("colorUsingColorSpace:"), srgb);
        if (converted == IntPtr.Zero)
        {
            return null;
        }

        return new RgbColor(
            ToByte(SendDouble(converted, Selector.GetHandle("redComponent"))),
            ToByte(SendDouble(converted, Selector.GetHandle("greenComponent"))),
            ToByte(SendDouble(converted, Selector.GetHandle("blueComponent"))));
    }

    private static RgbColor? ReadAccentDefault()
    {
        var value = NSUserDefaults.StandardUserDefaults.ValueForKey(new NSString(AccentColorKey)) as NSNumber;
        return AppleAccentPalette.FromAppleAccentColor(value?.Int32Value);
    }

    private static byte ToByte(double component) => (byte)Math.Round(Math.Clamp(component, 0, 1) * byte.MaxValue);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern double SendDouble(IntPtr receiver, IntPtr selector);
}
