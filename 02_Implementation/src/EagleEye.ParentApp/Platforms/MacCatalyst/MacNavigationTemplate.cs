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
/// The Catalyst CollectionView puts a newly selected item into "Selected" but never returns the previous one to
/// "Normal", so every item clicked once stayed coloured (US-006 test run 01, general feedback). Each item therefore
/// follows <see cref="MainViewModel.SelectedItem"/> itself.
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
        var group = new VisualStateGroup { Name = nameof(VisualStateManager.CommonStates) };
        group.States.Add(new VisualState { Name = VisualStateManager.CommonStates.Normal });
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
