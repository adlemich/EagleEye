using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Services;
using Microsoft.Maui.Controls.Internals;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// Menu item template of the Mac app (US-007 Step 1 review R-8, G-16): the selected item in the accent colour with a
/// readable text colour (the heading-bar resources), instead of Catalyst's light grey selection. A "Selected" visual
/// state also replaces the native selection background. Windows keeps the XAML template (AC-35).
/// </summary>
public static class MacNavigationTemplate
{
    private const double ItemFontSize = 15;
    private static readonly Thickness ItemPadding = new(20, 10);

    /// <summary>Creates the template.</summary>
    public static DataTemplate Create() => new(() =>
    {
        var label = new Label { Padding = ItemPadding, FontSize = ItemFontSize };
        label.SetBinding(Label.TextProperty, static (NavigationItem item) => item.Title);

        var selected = new VisualState { Name = VisualStateManager.CommonStates.Selected };
        selected.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = new DynamicResource(HeadingBarColorService.BackgroundKey) });
        selected.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = new DynamicResource(HeadingBarColorService.TextKey) });
        var group = new VisualStateGroup { Name = nameof(VisualStateManager.CommonStates) };
        group.States.Add(new VisualState { Name = VisualStateManager.CommonStates.Normal });
        group.States.Add(selected);
        VisualStateManager.SetVisualStateGroups(label, [group]);
        return label;
    });
}
