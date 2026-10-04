namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>An entry of the navigation menu.</summary>
/// <param name="Key">Stable key of the page.</param>
/// <param name="Title">Localized title.</param>
public sealed record NavigationItem(string Key, string Title);
