using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Services;
using Microsoft.Maui.Controls.Internals;

namespace EagleEye.ParentApp.Platforms.MacCatalyst;

/// <summary>
/// Menu item template of the Mac app (US-007 Step 1 review R-8, G-16): the selected item in the accent colour with a
/// readable text colour (the heading-bar resources), instead of Catalyst's light grey selection. A "Selected" visual
/// state also replaces the native selection background. Windows keeps the XAML template (AC-35).
/// </summary>
/// <remarks>
/// Every item clicked once stayed coloured (US-006 test run 01, general feedback): "Normal" needs an explicit
/// background (see below). Each item also follows <see cref="MainViewModel.SelectedItem"/> itself, so its state does
/// not depend on the Catalyst list's cell selection.
/// </remarks>
public static class MacNavigationTemplate
{
    private const double ItemFontSize = 15;
    private static readonly Thickness ItemPadding = new(20, 10);

    /// <summary>Creates the template for the menu of <paramref name="viewModel"/>.</summary>
    public static DataTemplate Create(MainViewModel viewModel) => new(() =>
    {
        var label = new Label { Padding = ItemPadding, FontSize = ItemFontSize };
        label.SetBinding(Label.TextProperty, static (NavigationItem item) => item.Title);

        var selected = new VisualState { Name = VisualStateManager.CommonStates.Selected };
        selected.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = new DynamicResource(HeadingBarColorService.BackgroundKey) });
        selected.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = new DynamicResource(HeadingBarColorService.TextKey) });
        // Explicit transparent background: when the Selected setter is removed, the background becomes unset, and an
        // unset background does not repaint the native view on Mac Catalyst, so the accent colour stayed.
        var normal = new VisualState { Name = VisualStateManager.CommonStates.Normal };
        normal.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent });
        var group = new VisualStateGroup { Name = nameof(VisualStateManager.CommonStates) };
        group.States.Add(normal);
        group.States.Add(selected);
        VisualStateManager.SetVisualStateGroups(label, [group]);

        // The menu has three items and lives as long as the view model, so the subscription is never removed.
        void UpdateState() => VisualStateManager.GoToState(label, ReferenceEquals(label.BindingContext, viewModel.SelectedItem)
            ? VisualStateManager.CommonStates.Selected
            : VisualStateManager.CommonStates.Normal);
        label.BindingContextChanged += (_, _) => UpdateState();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedItem))
            {
                UpdateState();
            }
        };
        return label;
    });
}
