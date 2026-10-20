using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// Mac-only control fixes (US-007 Step 1 review R-8; Decision 8, preference 2: handler mappings). Windows keeps its
/// look (AC-35).
/// <list type="bullet">
/// <item>G-12: an <see cref="Entry"/> in a table cell (parent <see cref="Grid"/>) ignores the style's minimum width of
/// 320, which WinUI clips at the cell and Catalyst draws over the next columns: it keeps its own width, or fills the
/// cell when it has none.</item>
/// <item>G-13: <see cref="Editor"/> gets a border.</item>
/// <item>G-15: <see cref="Picker"/> gets the theme's field colours, a border and a chevron.</item>
/// </list>
/// Not unit-tested (platform code); verified visually.
/// </summary>
public static class MacControlMappings
{
    private const float BorderWidth = 1;
    private const float CornerRadius = 4;
    private const float ChevronPadding = 8;
    private const string ChevronSymbol = "chevron.up.chevron.down";

    // A mid grey that reads as a field border on both the light and the dark page background.
    private static readonly UIColor BorderColor = UIColor.FromRGB(0x7F, 0x88, 0x83);

    // Colors.xaml PanelBackground/Text, light and dark.
    private static readonly UIColor FieldBackground = Dynamic(UIColor.FromRGB(0xF2, 0xF4, 0xF3), UIColor.FromRGB(0x1F, 0x24, 0x21));
    private static readonly UIColor FieldText = Dynamic(UIColor.FromRGB(0x1B, 0x1F, 0x1C), UIColor.FromRGB(0xE8, 0xEC, 0xEA));

    /// <summary>Registers the mappings (once, before the first window).</summary>
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("EagleEyeMacCellWidth", (_, view) =>
        {
            if (view is Entry entry)
            {
                FitToCell(entry);
                entry.ParentChanged -= OnEntryParentChanged;
                entry.ParentChanged += OnEntryParentChanged;
            }
        });
        EditorHandler.Mapper.AppendToMapping("EagleEyeMacBorder", (handler, _) => AddBorder(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("EagleEyeMacPicker", (handler, _) => StylePicker(handler.PlatformView));
    }

    private static void OnEntryParentChanged(object? sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
            FitToCell(entry);
        }
    }

    private static void FitToCell(Entry entry)
    {
        if (entry.Parent is not Grid)
        {
            return;
        }

        var hasWidth = entry.WidthRequest >= 0;
        entry.MinimumWidthRequest = hasWidth ? entry.WidthRequest : 0;
        if (!hasWidth)
        {
            entry.HorizontalOptions = LayoutOptions.Fill;
        }
    }

    private static void AddBorder(UIView view)
    {
        view.Layer.BorderWidth = BorderWidth;
        view.Layer.BorderColor = BorderColor.CGColor;
        view.Layer.CornerRadius = CornerRadius;
        view.ClipsToBounds = true;
    }

    private static void StylePicker(UIView view)
    {
        AddBorder(view);
        view.BackgroundColor = FieldBackground;
        if (view is not UITextField field)
        {
            return;
        }

        field.TextColor = FieldText;
        if (field.RightView is null && UIImage.GetSystemImage(ChevronSymbol) is { } chevron)
        {
            var image = new UIImageView(chevron) { TintColor = FieldText, ContentMode = UIViewContentMode.Center };
            image.Frame = new CoreGraphics.CGRect(0, 0, chevron.Size.Width + (2 * ChevronPadding), chevron.Size.Height);
            field.RightView = image;
            field.RightViewMode = UITextFieldViewMode.Always;
        }
    }

    private static UIColor Dynamic(UIColor light, UIColor dark)
        => new(traits => traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? dark : light);
}
